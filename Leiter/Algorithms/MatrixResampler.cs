namespace Leiter.Algorithms;

using System.Numerics;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Provides bilinear, box, and nearest-neighbor matrix resizing and sampling operations.
/// </summary>
public static class MatrixResampler
{
    private readonly record struct BilinearSampleAxis(
        int LowerIndex,
        int UpperIndex,
        double LowerWeight,
        double UpperWeight);

    private readonly record struct BilinearFootprint(
        int X0,
        int X1,
        int Y0,
        int Y1,
        double W00,
        double W10,
        double W01,
        double W11)
    {
        public static BilinearFootprint FromAxes(BilinearSampleAxis xAxis, BilinearSampleAxis yAxis) =>
            new(
                xAxis.LowerIndex,
                xAxis.UpperIndex,
                yAxis.LowerIndex,
                yAxis.UpperIndex,
                xAxis.LowerWeight * yAxis.LowerWeight,
                xAxis.UpperWeight * yAxis.LowerWeight,
                xAxis.LowerWeight * yAxis.UpperWeight,
                xAxis.UpperWeight * yAxis.UpperWeight);
    }

    /// <summary>
    /// Resizes a numeric pixel matrix to <paramref name="targetSize"/> using pixel-center bilinear interpolation.
    /// </summary>
    /// <typeparam name="T">The numeric pixel type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to resample.</param>
    /// <param name="targetSize">The target matrix dimensions.</param>
    /// <returns>A new <see cref="Matrix{T}"/> with dimensions <paramref name="targetSize"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetSize"/> has a negative width or height.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty while <paramref name="targetSize"/> has positive dimensions.</exception>
    public static Matrix<T> ResizeBilinear<T>(this IReadOnlyMatrix<T> source, Size targetSize)
        where T : struct, IPixel<T>
    {
        if (!ValidateResampleInputs(source, targetSize))
            return new SequentialMatrix<T>(targetSize);

        var result = new SequentialMatrix<T>(targetSize);
        var horizontalAxes = new BilinearSampleAxis[targetSize.Width];
        for (int targetX = 0; targetX < targetSize.Width; targetX++)
            horizontalAxes[targetX] = ComputeResizeBilinearAxis(targetX, source.Width, targetSize.Width);

        Parallel.For(0, targetSize.Height, targetY =>
        {
            BilinearSampleAxis vertical = ComputeResizeBilinearAxis(targetY, source.Height, targetSize.Height);

            for (int targetX = 0; targetX < targetSize.Width; targetX++)
            {
                BilinearFootprint fp = BilinearFootprint.FromAxes(horizontalAxes[targetX], vertical);
                result[targetX, targetY] = SampleFootprint(source, in fp);
            }
        });

        return result;
    }

    /// <summary>
    /// Resizes a numeric pixel matrix to <paramref name="targetSize"/> using area-averaged box resampling.
    /// </summary>
    /// <typeparam name="T">The numeric pixel type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to resample.</param>
    /// <param name="targetSize">The target matrix dimensions.</param>
    /// <returns>A new <see cref="Matrix{T}"/> with dimensions <paramref name="targetSize"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetSize"/> has a negative width or height.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty while <paramref name="targetSize"/> has positive dimensions.</exception>
    public static Matrix<T> ResizeBox<T>(this IReadOnlyMatrix<T> source, Size targetSize)
        where T : struct, IPixel<T>
    {
        if (!ValidateResampleInputs(source, targetSize))
            return new SequentialMatrix<T>(targetSize);

        if (targetSize == source.Size)
            return source.Clone();

        if (typeof(T) == typeof(Rgb8) && source is IReadOnlyMatrix<Rgb8> rgbSource)
            return (Matrix<T>)(object)ResizeBoxCore<Rgb8, Rgb8BoxAccumulator>(rgbSource, targetSize);

        if (typeof(T) == typeof(Lab32) && source is IReadOnlyMatrix<Lab32> labSource)
            return (Matrix<T>)(object)ResizeBoxCore<Lab32, Lab32BoxAccumulator>(labSource, targetSize);

        return ResizeBoxCore<T, VectorBoxAccumulator<T>>(source, targetSize);
    }

    /// <summary>
    /// Resizes a matrix to <paramref name="targetSize"/> using nearest-neighbor sampling.
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to resample.</param>
    /// <param name="targetSize">The target matrix dimensions.</param>
    /// <returns>A new <see cref="Matrix{T}"/> with dimensions <paramref name="targetSize"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetSize"/> has a negative width or height.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty while <paramref name="targetSize"/> has positive dimensions.</exception>
    public static Matrix<T> ResizeNearest<T>(this IReadOnlyMatrix<T> source, Size targetSize)
        where T : struct
    {
        if (!ValidateResampleInputs(source, targetSize))
            return new SequentialMatrix<T>(targetSize);

        var result = new SequentialMatrix<T>(targetSize);
        CopyNearest(source, result);
        return result;
    }

    /// <summary>
    /// Resizes a boolean mask matrix to <paramref name="targetSize"/> using nearest-neighbor sampling.
    /// </summary>
    /// <param name="source">The source boolean mask to resample.</param>
    /// <param name="targetSize">The target mask dimensions.</param>
    /// <returns>A new <see cref="MaskMatrix"/> with dimensions <paramref name="targetSize"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetSize"/> has a negative width or height.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty while <paramref name="targetSize"/> has positive dimensions.</exception>
    public static MaskMatrix ResizeMaskNearest(this IReadOnlyMatrix<bool> source, Size targetSize)
    {
        if (!ValidateResampleInputs(source, targetSize))
            return new MaskMatrix(targetSize);

        var result = new MaskMatrix(targetSize);
        CopyNearest(source, result);
        return result;
    }

    /// <summary>
    /// Samples a numeric pixel from the matrix at the specified coordinates using bilinear interpolation with edge clamping.
    /// </summary>
    /// <typeparam name="T">The numeric pixel type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to sample.</param>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <param name="edgeHandling">The edge handling mode for out-of-bounds coordinates.</param>
    /// <returns>The bilinearly interpolated pixel value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="edgeHandling"/> is invalid, or <paramref name="x"/> or <paramref name="y"/> is not finite.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty.</exception>
    public static T SampleBilinear<T>(
        this IReadOnlyMatrix<T> source,
        double x,
        double y,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
        where T : struct, IPixel<T>
    {
        ValidateSampleInputs(source, x, y, edgeHandling);
        return SampleBilinearUnchecked(source, x, y, edgeHandling);
    }

    internal static T SampleBilinearUnchecked<T>(
        IReadOnlyMatrix<T> source,
        double x,
        double y,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
        where T : struct, IPixel<T>
    {
        BilinearFootprint fp = ComputeBilinearFootprint(source.Width, source.Height, x, y, edgeHandling);
        return SampleFootprint(source, in fp);
    }

    private static T SampleFootprint<T>(IReadOnlyMatrix<T> source, in BilinearFootprint fp)
        where T : struct, IPixel<T>
    {
        Vector<double> interpolatedPixel =
            (source[fp.X0, fp.Y0].ToDoubleVector() * fp.W00) +
            (source[fp.X1, fp.Y0].ToDoubleVector() * fp.W10) +
            (source[fp.X0, fp.Y1].ToDoubleVector() * fp.W01) +
            (source[fp.X1, fp.Y1].ToDoubleVector() * fp.W11);

        return T.FromDoubleVector(interpolatedPixel);
    }

    /// <summary>
    /// Samples a numeric pixel from the matrix at the specified coordinates using bilinear interpolation
    /// restricted to 2×2 corner neighbors satisfying <paramref name="predicate"/>.
    /// </summary>
    /// <typeparam name="T">The numeric pixel type stored in the matrix.</typeparam>
    /// <typeparam name="TState">A state value passed to <paramref name="predicate"/> to avoid closure allocations.</typeparam>
    /// <param name="source">The source matrix to sample.</param>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <param name="state">Caller state passed to <paramref name="predicate"/>.</param>
    /// <param name="predicate">Predicate determining whether each integer neighbor coordinate contributes to interpolation.</param>
    /// <param name="fallback">The value returned when no contributing neighbor satisfies <paramref name="predicate"/>.</param>
    /// <param name="edgeHandling">The edge handling mode for out-of-bounds coordinates.</param>
    /// <returns>The normalized weighted bilinear sample, or <paramref name="fallback"/> if all matching weights sum to zero.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="edgeHandling"/> is invalid, or <paramref name="x"/> or <paramref name="y"/> is not finite.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty.</exception>
    public static T SampleBilinearWhere<T, TState>(
        this IReadOnlyMatrix<T> source,
        double x,
        double y,
        TState state,
        Func<int, int, TState, bool> predicate,
        T fallback = default,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ValidateSampleInputs(source, x, y, edgeHandling);

        return SampleBilinearWhereUnchecked(source, x, y, state, predicate, fallback, edgeHandling);
    }

    internal static T SampleBilinearWhereUnchecked<T, TState>(
        IReadOnlyMatrix<T> source,
        double x,
        double y,
        TState state,
        Func<int, int, TState, bool> predicate,
        T fallback = default,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
        where T : struct, IPixel<T>
    {
        BilinearFootprint fp = ComputeBilinearFootprint(source.Width, source.Height, x, y, edgeHandling);

        Vector<double> sum = Vector<double>.Zero;
        double weightSum = 0.0;

        if (predicate(fp.X0, fp.Y0, state))
        {
            sum += source[fp.X0, fp.Y0].ToDoubleVector() * fp.W00;
            weightSum += fp.W00;
        }

        if (predicate(fp.X1, fp.Y0, state))
        {
            sum += source[fp.X1, fp.Y0].ToDoubleVector() * fp.W10;
            weightSum += fp.W10;
        }

        if (predicate(fp.X0, fp.Y1, state))
        {
            sum += source[fp.X0, fp.Y1].ToDoubleVector() * fp.W01;
            weightSum += fp.W01;
        }

        if (predicate(fp.X1, fp.Y1, state))
        {
            sum += source[fp.X1, fp.Y1].ToDoubleVector() * fp.W11;
            weightSum += fp.W11;
        }

        return weightSum > 0.0
            ? T.FromDoubleVector(sum / weightSum)
            : fallback;
    }

    internal static Rgb64 SampleBilinearRgb64WhereUnchecked<TState>(
        IReadOnlyMatrix<Rgb8> source,
        double x,
        double y,
        TState state,
        Func<int, int, TState, bool> predicate,
        Rgb64 fallback = default,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
    {
        BilinearFootprint fp = ComputeBilinearFootprint(source.Width, source.Height, x, y, edgeHandling);

        double sumR = 0.0;
        double sumG = 0.0;
        double sumB = 0.0;
        double weightSum = 0.0;

        if (predicate(fp.X0, fp.Y0, state))
        {
            Rgb8 p = source[fp.X0, fp.Y0];
            sumR += p.R * fp.W00;
            sumG += p.G * fp.W00;
            sumB += p.B * fp.W00;
            weightSum += fp.W00;
        }

        if (predicate(fp.X1, fp.Y0, state))
        {
            Rgb8 p = source[fp.X1, fp.Y0];
            sumR += p.R * fp.W10;
            sumG += p.G * fp.W10;
            sumB += p.B * fp.W10;
            weightSum += fp.W10;
        }

        if (predicate(fp.X0, fp.Y1, state))
        {
            Rgb8 p = source[fp.X0, fp.Y1];
            sumR += p.R * fp.W01;
            sumG += p.G * fp.W01;
            sumB += p.B * fp.W01;
            weightSum += fp.W01;
        }

        if (predicate(fp.X1, fp.Y1, state))
        {
            Rgb8 p = source[fp.X1, fp.Y1];
            sumR += p.R * fp.W11;
            sumG += p.G * fp.W11;
            sumB += p.B * fp.W11;
            weightSum += fp.W11;
        }

        if (weightSum <= 0.0)
            return fallback;

        double invScale = 1.0 / (weightSum * 255.0);
        return new Rgb64(sumR * invScale, sumG * invScale, sumB * invScale);
    }

    /// <summary>
    /// Samples a numeric pixel from the matrix at the specified coordinates using bilinear interpolation
    /// restricted to 2×2 corner neighbors satisfying <paramref name="predicate"/>.
    /// </summary>
    /// <typeparam name="T">The numeric pixel type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to sample.</param>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <param name="predicate">Predicate determining whether each integer neighbor coordinate contributes to interpolation.</param>
    /// <param name="fallback">The value returned when no contributing neighbor satisfies <paramref name="predicate"/>.</param>
    /// <param name="edgeHandling">The edge handling mode for out-of-bounds coordinates.</param>
    /// <returns>The normalized weighted bilinear sample, or <paramref name="fallback"/> if all matching weights sum to zero.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="edgeHandling"/> is invalid, or <paramref name="x"/> or <paramref name="y"/> is not finite.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty.</exception>
    public static T SampleBilinearWhere<T>(
        this IReadOnlyMatrix<T> source,
        double x,
        double y,
        Func<int, int, bool> predicate,
        T fallback = default,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return source.SampleBilinearWhere(x, y, predicate, static (cx, cy, pred) => pred(cx, cy), fallback, edgeHandling);
    }

    /// <summary>
    /// Warps a numeric pixel matrix using per-pixel 2D displacement vectors sampled with bilinear interpolation.
    /// Non-finite displacement components are treated as zero displacement along that axis.
    /// </summary>
    /// <typeparam name="T">The numeric pixel type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to warp.</param>
    /// <param name="displacementField">The per-pixel 2D offset vectors matching <paramref name="source"/> dimensions.</param>
    /// <param name="scale">Scalar multiplier applied to each displacement vector.</param>
    /// <param name="edgeHandling">The edge handling mode for out-of-bounds coordinates.</param>
    /// <returns>A new <see cref="Matrix{T}"/> containing the warped image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="displacementField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="displacementField"/> dimensions do not match <paramref name="source"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scale"/> is not finite or <paramref name="edgeHandling"/> is invalid.</exception>
    public static Matrix<T> WarpBilinear<T>(
        this IReadOnlyMatrix<T> source,
        IReadOnlyMatrix<Vector2D> displacementField,
        double scale = 1.0,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfSizeMismatch(source, displacementField);
        Guard.ThrowIfNotFinite(scale);
        Guard.ThrowIfUndefinedEnum(edgeHandling);

        if (source.Count == 0)
            return new SequentialMatrix<T>(source.Size);

        if (scale == 0.0)
            return source.Clone();

        int width = source.Width;
        int height = source.Height;
        var result = new SequentialMatrix<T>(width, height);

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                Vector2D d = displacementField[x, y];
                double sampleX = double.IsFinite(d.X) ? x + (scale * d.X) : x;
                double sampleY = double.IsFinite(d.Y) ? y + (scale * d.Y) : y;
                result[x, y] = SampleBilinearUnchecked(source, sampleX, sampleY, edgeHandling);
            }
        });

        return result;
    }

    /// <summary>
    /// Samples a numeric pixel from the matrix at the specified position using bilinear interpolation with edge clamping.
    /// </summary>
    /// <typeparam name="T">The numeric pixel type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to sample.</param>
    /// <param name="position">The 2D coordinate position.</param>
    /// <param name="edgeHandling">The edge handling mode for out-of-bounds coordinates.</param>
    /// <returns>The bilinearly interpolated pixel value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="edgeHandling"/> is invalid, or <paramref name="position"/> has non-finite components.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty.</exception>
    public static T SampleBilinear<T>(
        this IReadOnlyMatrix<T> source,
        Vector2D position,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
        where T : struct, IPixel<T> =>
        source.SampleBilinear(position.X, position.Y, edgeHandling);

    /// <summary>
    /// Samples an element from the matrix at the specified coordinates using nearest-neighbor sampling with edge clamping.
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to sample.</param>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <param name="edgeHandling">The edge handling mode for out-of-bounds coordinates.</param>
    /// <returns>The nearest element in the matrix.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="edgeHandling"/> is invalid, or <paramref name="x"/> or <paramref name="y"/> is not finite.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty.</exception>
    public static T SampleNearest<T>(
        this IReadOnlyMatrix<T> source,
        double x,
        double y,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
        where T : struct
    {
        ValidateSampleInputs(source, x, y, edgeHandling);
        return SampleNearestUnchecked(source, x, y, edgeHandling);
    }

    internal static T SampleNearestUnchecked<T>(
        IReadOnlyMatrix<T> source,
        double x,
        double y,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
        where T : struct
    {
        int ix = edgeHandling.ResolveIndex((int)Math.Round(x, MidpointRounding.AwayFromZero), source.Width);
        int iy = edgeHandling.ResolveIndex((int)Math.Round(y, MidpointRounding.AwayFromZero), source.Height);

        return source[ix, iy];
    }

    /// <summary>
    /// Samples an element from the matrix at the specified position using nearest-neighbor sampling with edge clamping.
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to sample.</param>
    /// <param name="position">The 2D coordinate position.</param>
    /// <param name="edgeHandling">The edge handling mode for out-of-bounds coordinates.</param>
    /// <returns>The nearest element in the matrix.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="edgeHandling"/> is invalid, or <paramref name="position"/> has non-finite components.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty.</exception>
    public static T SampleNearest<T>(
        this IReadOnlyMatrix<T> source,
        Vector2D position,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
        where T : struct =>
        source.SampleNearest(position.X, position.Y, edgeHandling);

    private static void ValidateSampleInputs(
        IUntypedMatrix source,
        double x,
        double y,
        EdgeHandling edgeHandling)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(edgeHandling);

        if (source.Count == 0)
            throw new ArgumentException("Cannot sample from an empty matrix.", nameof(source));

        Guard.ThrowIfNotFinite(x);
        Guard.ThrowIfNotFinite(y);
    }

    private static BilinearFootprint ComputeBilinearFootprint(
        int width,
        int height,
        double x,
        double y,
        EdgeHandling edgeHandling = EdgeHandling.Extend) =>
        BilinearFootprint.FromAxes(
            ComputeBilinearSampleAxis(x, width, edgeHandling),
            ComputeBilinearSampleAxis(y, height, edgeHandling));

    private static bool ValidateResampleInputs(IUntypedMatrix source, Size targetSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfNegative(targetSize);

        if (targetSize.Width == 0 || targetSize.Height == 0)
            return false;

        if (source.Count == 0)
            throw new ArgumentException("Cannot resample an empty source matrix to a non-empty target size.", nameof(source));

        return true;
    }

    private static BilinearSampleAxis ComputeResizeBilinearAxis(
        int targetCoord,
        int sourceLength,
        int targetLength)
    {
        double sourceCoord = (((targetCoord + 0.5) * sourceLength) / targetLength) - 0.5;
        return ComputeBilinearSampleAxis(sourceCoord, sourceLength);
    }

    private static BilinearSampleAxis ComputeBilinearSampleAxis(
        double sourceCoord,
        int sourceLength,
        EdgeHandling edgeHandling = EdgeHandling.Extend)
    {
        double resolvedCoord = edgeHandling.ResolveCoordinate(sourceCoord, sourceLength);

        int lowerIndex = (int)Math.Floor(resolvedCoord);
        int upperIndex = edgeHandling.ResolveIndex(lowerIndex + 1, sourceLength);
        double upperWeight = resolvedCoord - lowerIndex;
        double lowerWeight = 1.0 - upperWeight;

        return new BilinearSampleAxis(lowerIndex, upperIndex, lowerWeight, upperWeight);
    }

    private static (int Start, int End) ComputeBoxAxisSpan(int targetCoord, int sourceLength, int targetLength)
    {
        int start = Math.Min((int)((long)targetCoord * sourceLength / targetLength), sourceLength - 1);
        int end = Math.Clamp((int)((long)(targetCoord + 1) * sourceLength / targetLength), start + 1, sourceLength);
        return (start, end);
    }

    private interface IBoxAccumulator<T>
        where T : struct
    {
        void Add(T pixel);
        T ComputeAverage(double invCount);
    }

    private struct Rgb8BoxAccumulator : IBoxAccumulator<Rgb8>
    {
        private long sumR;
        private long sumG;
        private long sumB;

        public void Add(Rgb8 pixel)
        {
            sumR += pixel.R;
            sumG += pixel.G;
            sumB += pixel.B;
        }

        public readonly Rgb8 ComputeAverage(double invCount) =>
            new(
                Rgb8.ClampRoundedByte(sumR * invCount),
                Rgb8.ClampRoundedByte(sumG * invCount),
                Rgb8.ClampRoundedByte(sumB * invCount));
    }

    private struct Lab32BoxAccumulator : IBoxAccumulator<Lab32>
    {
        private double sumL;
        private double sumA;
        private double sumB;

        public void Add(Lab32 pixel)
        {
            sumL += pixel.L;
            sumA += pixel.A;
            sumB += pixel.B;
        }

        public readonly Lab32 ComputeAverage(double invCount) =>
            new(
                (float)(sumL * invCount),
                (float)(sumA * invCount),
                (float)(sumB * invCount));
    }

    private struct VectorBoxAccumulator<T> : IBoxAccumulator<T>
        where T : struct, IPixel<T>
    {
        private Vector<double> sum;

        public void Add(T pixel) => sum += pixel.ToDoubleVector();

        public readonly T ComputeAverage(double invCount) => T.FromDoubleVector(sum * invCount);
    }

    private static Matrix<T> ResizeBoxCore<T, TAccumulator>(IReadOnlyMatrix<T> source, Size targetSize)
        where T : struct
        where TAccumulator : struct, IBoxAccumulator<T>
    {
        int srcWidth = source.Width;
        int srcHeight = source.Height;
        int dstWidth = targetSize.Width;
        int dstHeight = targetSize.Height;
        var result = new SequentialMatrix<T>(targetSize);
        var xSpans = new (int Start, int End)[dstWidth];
        for (int tx = 0; tx < dstWidth; tx++)
            xSpans[tx] = ComputeBoxAxisSpan(tx, srcWidth, dstWidth);

        Parallel.For(0, dstHeight, ty =>
        {
            (int y0, int y1) = ComputeBoxAxisSpan(ty, srcHeight, dstHeight);
            double spanHeight = y1 - y0;
            for (int tx = 0; tx < dstWidth; tx++)
            {
                (int x0, int x1) = xSpans[tx];
                TAccumulator acc = default;

                for (int sy = y0; sy < y1; sy++)
                {
                    for (int sx = x0; sx < x1; sx++)
                    {
                        acc.Add(source[sx, sy]);
                    }
                }

                double invCount = 1.0 / ((x1 - x0) * spanHeight);
                result[tx, ty] = acc.ComputeAverage(invCount);
            }
        });

        return result;
    }

    private static int ComputeNearestSourceIndex(int targetCoord, int sourceLength, int targetLength) =>
        Math.Clamp((int)((long)targetCoord * sourceLength / targetLength), 0, sourceLength - 1);

    private static void CopyNearest<T>(IReadOnlyMatrix<T> source, Matrix<T> destination)
        where T : struct
    {
        int sourceWidth = source.Width;
        int sourceHeight = source.Height;
        int targetWidth = destination.Width;
        int targetHeight = destination.Height;
        var sourceXMap = new int[targetWidth];
        for (int targetX = 0; targetX < targetWidth; targetX++)
            sourceXMap[targetX] = ComputeNearestSourceIndex(targetX, sourceWidth, targetWidth);

        Parallel.For(0, targetHeight, targetY =>
        {
            int sourceY = ComputeNearestSourceIndex(targetY, sourceHeight, targetHeight);
            for (int targetX = 0; targetX < targetWidth; targetX++)
            {
                destination[targetX, targetY] = source[sourceXMap[targetX], sourceY];
            }
        });
    }
}

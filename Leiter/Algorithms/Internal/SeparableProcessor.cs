namespace Leiter.Algorithms.Internal;

using System;
using System.Runtime.Intrinsics;
using System.Threading.Tasks;
using Leiter.Core;

/// <summary>
/// Provides shared parallel two-pass (row-then-column) separable line processing over vectorized pixel buffers.
/// </summary>
internal static class SeparableProcessor
{
    /// <summary>
    /// Represents a method that applies an in-place one-dimensional filter to a line of SIMD vector samples.
    /// </summary>
    /// <param name="line">The line of SIMD vectors to filter in place.</param>
    internal delegate void LineFilter1D(Span<Vector256<double>> line);

    private interface ILineOperator
    {
        bool NeedsSeparateSource { get; }
        void ApplyHorizontal(ReadOnlySpan<Vector256<double>> source, Span<Vector256<double>> destination);
        void ApplyVertical(ReadOnlySpan<Vector256<double>> source, Span<Vector256<double>> destination);
    }

    private readonly struct InPlaceLineOperator(LineFilter1D filterLine) : ILineOperator
    {
        public bool NeedsSeparateSource => false;

        public void ApplyHorizontal(ReadOnlySpan<Vector256<double>> source, Span<Vector256<double>> destination) =>
            filterLine(destination);

        public void ApplyVertical(ReadOnlySpan<Vector256<double>> source, Span<Vector256<double>> destination) =>
            filterLine(destination);
    }

    private readonly struct SeparableConvolutionOperator(double[] horizontalKernel, double[] verticalKernel) : ILineOperator
    {
        public bool NeedsSeparateSource => true;

        public void ApplyHorizontal(ReadOnlySpan<Vector256<double>> source, Span<Vector256<double>> destination) =>
            ConvolveLine(source, destination, horizontalKernel);

        public void ApplyVertical(ReadOnlySpan<Vector256<double>> source, Span<Vector256<double>> destination) =>
            ConvolveLine(source, destination, verticalKernel);
    }

    /// <summary>
    /// Applies a symmetric in-place 1D line filter across rows and then columns of <paramref name="image"/>.
    /// </summary>
    public static Matrix<T> Apply<T>(IReadOnlyMatrix<T> image, LineFilter1D filterLine)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(filterLine);
        return ApplyCore(image, new InPlaceLineOperator(filterLine));
    }

    /// <summary>
    /// Convolves <paramref name="image"/> with separable 1D horizontal and vertical kernels using clamp-to-edge border handling.
    /// </summary>
    public static Matrix<T> Convolve<T>(
        IReadOnlyMatrix<T> image,
        ReadOnlySpan<double> horizontalKernel,
        ReadOnlySpan<double> verticalKernel)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(image);

        if (horizontalKernel.IsEmpty)
            throw new ArgumentException("Horizontal kernel must not be empty.", nameof(horizontalKernel));
        if (verticalKernel.IsEmpty)
            throw new ArgumentException("Vertical kernel must not be empty.", nameof(verticalKernel));

        if (image.Width == 0 || image.Height == 0)
            return new SequentialMatrix<T>(image.Size);

        return ApplyCore(image, new SeparableConvolutionOperator(horizontalKernel.ToArray(), verticalKernel.ToArray()));
    }

    private static Matrix<T> ApplyCore<T, TOperator>(IReadOnlyMatrix<T> image, TOperator lineOperator)
        where T : struct, IPixel<T>
        where TOperator : struct, ILineOperator
    {
        ArgumentNullException.ThrowIfNull(image);

        Size size = image.Size;
        int width = size.Width;
        int height = size.Height;
        var result = new SequentialMatrix<T>(size);
        if (width == 0 || height == 0)
            return result;

        Vector256<double>[] buffer = image.MapToArray(static pixel => pixel.ToDoubleVector());

        if (lineOperator.NeedsSeparateSource)
        {
            Parallel.For(
                0,
                height,
                () => new Vector256<double>[width],
                (y, _, rowScratch) =>
                {
                    Span<Vector256<double>> row = buffer.AsSpan(size.IndexOf(0, y), width);
                    row.CopyTo(rowScratch);
                    lineOperator.ApplyHorizontal(rowScratch, row);
                    return rowScratch;
                },
                static _ => { });
        }
        else
        {
            Parallel.For(0, height, y =>
            {
                Span<Vector256<double>> row = buffer.AsSpan(size.IndexOf(0, y), width);
                lineOperator.ApplyHorizontal(row, row);
            });
        }

        Parallel.For(
            0,
            width,
            () => (Source: new Vector256<double>[height], Destination: lineOperator.NeedsSeparateSource ? new Vector256<double>[height] : null),
            (x, _, state) =>
            {
                Vector256<double>[] colSource = state.Source;
                Vector256<double>[] colDestination = state.Destination ?? colSource;

                for (int y = 0; y < height; y++)
                    colSource[y] = buffer[size.IndexOf(x, y)];

                lineOperator.ApplyVertical(colSource, colDestination);

                for (int y = 0; y < height; y++)
                    result[x, y] = T.FromDoubleVector(colDestination[y]);

                return state;
            },
            static _ => { });

        return result;
    }

    private static void ConvolveLine(
        ReadOnlySpan<Vector256<double>> source,
        Span<Vector256<double>> destination,
        double[] kernel)
    {
        int length = source.Length;
        int kernelLength = kernel.Length;
        int half = kernelLength / 2;

        for (int i = 0; i < length; i++)
        {
            Vector256<double> sum = Vector256<double>.Zero;
            for (int k = 0; k < kernelLength; k++)
            {
                int sampleIndex = EdgeHandling.Extend.ResolveIndex(i - half + k, length);
                sum += source[sampleIndex] * kernel[k];
            }

            destination[i] = sum;
        }
    }
}

namespace Leiter.Algorithms.Segmentation;

using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Implements Rother, Kolmogorov &amp; Blake's GrabCut foreground/background segmentation
/// using 3D RGB Gaussian Mixture Models and an 8-connected Boykov–Kolmogorov min-cut solver.
/// </summary>
public static class GrabCut
{
    /// <summary>
    /// Segments an image given an axis-aligned foreground bounding rectangle.
    /// Pixels inside the rectangle are initialized as <see cref="TrimapLabel.ProbableForeground"/> and pixels outside as <see cref="TrimapLabel.Background"/>.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="rectangleTopLeft">The top-left coordinate of the bounding rectangle.</param>
    /// <param name="rectangleSize">The width and height of the bounding rectangle.</param>
    /// <param name="options">Optional GrabCut tuning parameters.</param>
    /// <returns>A binary mask matrix with <c>1</c> for foreground pixels and <c>0</c> for background pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rectangleSize"/> dimensions are not positive or <paramref name="options"/> contains invalid values.</exception>
    /// <exception cref="ArgumentException">Thrown when the rectangle does not overlap the image or leaves zero background pixels outside it.</exception>
    public static Matrix<LongPixel> Segment(
        IReadOnlyMatrix<Rgb8> image,
        Coord rectangleTopLeft,
        Size rectangleSize,
        GrabCutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        GrabCutOptions resolvedOptions = options ?? new GrabCutOptions();
        resolvedOptions.Validate();

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rectangleSize.Width, 0, nameof(rectangleSize));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rectangleSize.Height, 0, nameof(rectangleSize));

        if (image.Count == 0)
            return new SequentialMatrix<LongPixel>(image.Size);

        int left = Math.Max(0, rectangleTopLeft.X);
        int top = Math.Max(0, rectangleTopLeft.Y);
        int right = (int)Math.Min((long)image.Width, (long)rectangleTopLeft.X + rectangleSize.Width);
        int bottom = (int)Math.Min((long)image.Height, (long)rectangleTopLeft.Y + rectangleSize.Height);

        if (left >= right || top >= bottom)
            throw new ArgumentException("Foreground rectangle does not overlap the image bounds.", nameof(rectangleTopLeft));

        int overlapArea = (right - left) * (bottom - top);
        if (overlapArea == image.Count)
            throw new ArgumentException("Foreground rectangle must leave at least one background pixel outside it.", nameof(rectangleSize));

        var trimap = new SequentialMatrix<LongPixel>(image.Size);
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
                trimap[x, y] = (long)TrimapLabel.ProbableForeground;
        }

        return Segment(image, trimap, resolvedOptions);
    }

    /// <summary>
    /// Segments an image using an explicit <see cref="TrimapLabel"/> matrix.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="trimap">A matrix of <see cref="TrimapLabel"/> values matching <paramref name="image"/> dimensions.</param>
    /// <param name="options">Optional GrabCut tuning parameters.</param>
    /// <returns>A binary mask matrix with <c>1</c> for foreground pixels and <c>0</c> for background pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="trimap"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="trimap"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="trimap"/> contains an invalid <see cref="TrimapLabel"/> value or <paramref name="options"/> is invalid.</exception>
    public static Matrix<LongPixel> Segment(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<LongPixel> trimap,
        GrabCutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(trimap);

        if (image.Size != trimap.Size)
            throw new ArgumentException("Image and trimap dimensions must match.", nameof(trimap));

        GrabCutOptions resolvedOptions = options ?? new GrabCutOptions();
        resolvedOptions.Validate();

        if (image.Count == 0)
            return new SequentialMatrix<LongPixel>(image.Size);

        var session = new GrabCutSession(image, resolvedOptions.MixtureComponentCount, resolvedOptions.SmoothnessWeight);
        return session.Run(trimap, resolvedOptions.Iterations);
    }
}

/// <summary>
/// Reusable per-image GrabCut graph and GMM state so multi-round pipelines such as
/// <see cref="Saliency.SaliencyCut"/> compute pairwise smoothness capacities only once.
/// </summary>
internal sealed class GrabCutSession
{
    private const double DiagonalDistanceFactor = 0.7071067811865475; // 1 / sqrt(2)

    private readonly int width;
    private readonly int height;
    private readonly int pixelCount;
    private readonly double smoothnessWeight;

    private readonly float[] interleavedRgb;
    private readonly GridMinCutSolver solver;
    private readonly GaussianMixtureModel foregroundGmm;
    private readonly GaussianMixtureModel backgroundGmm;

    private readonly byte[] trimapLabels;
    private readonly byte[] currentForeground;
    private readonly int[] componentAssignments;
    private readonly double[] sourceCapacities;
    private readonly double[] sinkCapacities;

    public GrabCutSession(IReadOnlyMatrix<Rgb8> image, int mixtureComponentCount, double smoothnessWeight)
    {
        width = image.Width;
        height = image.Height;
        pixelCount = image.Count;
        this.smoothnessWeight = smoothnessWeight;

        interleavedRgb = ExtractRgbArray(image, width, height);
        double beta = ComputeContrastParameterBeta(interleavedRgb, width, height);

        solver = new GridMinCutSolver(width, height);
        PopulateSmoothnessEdges(solver, interleavedRgb, width, height, smoothnessWeight, beta);

        foregroundGmm = new GaussianMixtureModel(mixtureComponentCount);
        backgroundGmm = new GaussianMixtureModel(mixtureComponentCount);

        trimapLabels = new byte[pixelCount];
        currentForeground = new byte[pixelCount];
        componentAssignments = new int[pixelCount];
        sourceCapacities = new double[pixelCount];
        sinkCapacities = new double[pixelCount];
    }

    public Matrix<LongPixel> Run(IReadOnlyMatrix<LongPixel> trimap, int iterations)
    {
        int initialFgCount = 0;
        int initialBgCount = 0;
        int hardBgCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                long rawLabel = trimap[x, y].Value;
                if ((ulong)rawLabel > (ulong)TrimapLabel.ProbableForeground)
                    throw new ArgumentOutOfRangeException(nameof(trimap), rawLabel, "Trimap values must correspond to valid TrimapLabel members (0..3).");

                byte label = (byte)rawLabel;
                int p = (y * width) + x;
                trimapLabels[p] = label;

                bool isForeground = label == (byte)TrimapLabel.Foreground || label == (byte)TrimapLabel.ProbableForeground;
                currentForeground[p] = isForeground ? (byte)1 : (byte)0;
                if (isForeground)
                {
                    initialFgCount++;
                }
                else
                {
                    initialBgCount++;
                    if (label == (byte)TrimapLabel.Background)
                        hardBgCount++;
                }
            }
        }

        if (initialFgCount == 0)
            return new SequentialMatrix<LongPixel>(width, height);

        if (initialBgCount == 0)
        {
            var allForeground = new SequentialMatrix<LongPixel>(width, height);
            allForeground.SetAll(1L);
            return allForeground;
        }

        InitializeGmms(initialFgCount, initialBgCount, hardBgCount);

        int fgCount = initialFgCount;
        int bgCount = initialBgCount;
        double hardWeight = (9.0 * smoothnessWeight) + 1.0;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            if (iteration > 0)
            {
                Parallel.For(0, pixelCount, p =>
                {
                    int rgbOffset = p * 3;
                    float r = interleavedRgb[rgbOffset + 0];
                    float g = interleavedRgb[rgbOffset + 1];
                    float b = interleavedRgb[rgbOffset + 2];

                    componentAssignments[p] = currentForeground[p] != 0
                        ? foregroundGmm.AssignBestComponent(r, g, b)
                        : backgroundGmm.AssignBestComponent(r, g, b);
                });

                foregroundGmm.FitFromAssignedComponents(interleavedRgb, currentForeground, 1, componentAssignments, fgCount);
                backgroundGmm.FitFromAssignedComponents(interleavedRgb, currentForeground, 0, componentAssignments, bgCount);
            }

            Parallel.For(0, pixelCount, p =>
            {
                byte label = trimapLabels[p];
                if (label == (byte)TrimapLabel.Foreground)
                {
                    sourceCapacities[p] = hardWeight;
                    sinkCapacities[p] = 0.0;
                }
                else if (label == (byte)TrimapLabel.Background)
                {
                    sourceCapacities[p] = 0.0;
                    sinkCapacities[p] = hardWeight;
                }
                else
                {
                    int rgbOffset = p * 3;
                    float r = interleavedRgb[rgbOffset + 0];
                    float g = interleavedRgb[rgbOffset + 1];
                    float b = interleavedRgb[rgbOffset + 2];

                    double bgNll = backgroundGmm.EvaluateNegativeLogLikelihood(r, g, b);
                    double fgNll = foregroundGmm.EvaluateNegativeLogLikelihood(r, g, b);
                    if (bgNll >= fgNll)
                    {
                        sourceCapacities[p] = bgNll - fgNll;
                        sinkCapacities[p] = 0.0;
                    }
                    else
                    {
                        sourceCapacities[p] = 0.0;
                        sinkCapacities[p] = fgNll - bgNll;
                    }
                }
            });

            solver.Solve(sourceCapacities, sinkCapacities);

            bool changed = false;
            fgCount = 0;

            for (int p = 0; p < pixelCount; p++)
            {
                byte label = trimapLabels[p];
                byte nextFg = label switch
                {
                    (byte)TrimapLabel.Foreground => 1,
                    (byte)TrimapLabel.Background => 0,
                    _ => solver.IsSourceSide(p) ? (byte)1 : (byte)0,
                };

                if (nextFg != currentForeground[p])
                {
                    currentForeground[p] = nextFg;
                    changed = true;
                }

                if (nextFg != 0)
                    fgCount++;
            }

            bgCount = pixelCount - fgCount;
            if (!changed || fgCount == 0 || bgCount == 0)
                break;
        }

        return BuildMaskFromState();
    }

    private void InitializeGmms(int initialFgCount, int initialBgCount, int hardBgCount)
    {
        var fgIndices = new int[initialFgCount];
        bool useHardBackgroundOnly = hardBgCount > 0;
        var bgIndices = new int[useHardBackgroundOnly ? hardBgCount : initialBgCount];
        int fgCursor = 0;
        int bgCursor = 0;

        for (int p = 0; p < pixelCount; p++)
        {
            if (currentForeground[p] != 0)
                fgIndices[fgCursor++] = p;
            else if (!useHardBackgroundOnly || trimapLabels[p] == (byte)TrimapLabel.Background)
                bgIndices[bgCursor++] = p;
        }

        foregroundGmm.InitializeAndFit(interleavedRgb, fgIndices);
        backgroundGmm.InitializeAndFit(interleavedRgb, bgIndices);
    }

    private Matrix<LongPixel> BuildMaskFromState()
    {
        var result = new SequentialMatrix<LongPixel>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                result[x, y] = currentForeground[(y * width) + x];
        }
        return result;
    }

    private static float[] ExtractRgbArray(IReadOnlyMatrix<Rgb8> image, int width, int height)
    {
        var rgb = new float[width * height * 3];
        Parallel.For(0, height, y =>
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                Rgb8 pixel = image[x, y];
                int offset = (rowBase + x) * 3;
                rgb[offset + 0] = pixel.R;
                rgb[offset + 1] = pixel.G;
                rgb[offset + 2] = pixel.B;
            }
        });
        return rgb;
    }

    private static double ComputeContrastParameterBeta(float[] rgb, int width, int height)
    {
        double sumSquaredDiff = 0.0;
        long pairCount = 0;

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int p = rowBase + x;
                if (x + 1 < width)
                {
                    sumSquaredDiff += ColorDistanceSquared(rgb, p, p + 1);
                    pairCount++;
                }

                if (y + 1 < height)
                {
                    int below = p + width;
                    sumSquaredDiff += ColorDistanceSquared(rgb, p, below);
                    pairCount++;

                    if (x + 1 < width)
                    {
                        sumSquaredDiff += ColorDistanceSquared(rgb, p, below + 1);
                        pairCount++;
                    }

                    if (x - 1 >= 0)
                    {
                        sumSquaredDiff += ColorDistanceSquared(rgb, p, below - 1);
                        pairCount++;
                    }
                }
            }
        }

        if (pairCount == 0)
            return 0.0;

        double meanDiffSq = sumSquaredDiff / pairCount;
        return meanDiffSq > 1e-12 ? 1.0 / (2.0 * meanDiffSq) : 0.0;
    }

    private static void PopulateSmoothnessEdges(
        GridMinCutSolver solver,
        float[] rgb,
        int width,
        int height,
        double smoothnessWeight,
        double beta)
    {
        double diagonalWeight = smoothnessWeight * DiagonalDistanceFactor;

        Parallel.For(0, height, y =>
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int p = rowBase + x;
                if (x + 1 < width)
                {
                    double diffSq = ColorDistanceSquared(rgb, p, p + 1);
                    solver.SetUndirectedEdgeCapacity(p, 0, (float)(smoothnessWeight * Math.Exp(-beta * diffSq)));
                }

                if (y + 1 < height)
                {
                    int below = p + width;
                    double verticalDiffSq = ColorDistanceSquared(rgb, p, below);
                    solver.SetUndirectedEdgeCapacity(p, 1, (float)(smoothnessWeight * Math.Exp(-beta * verticalDiffSq)));

                    if (x + 1 < width)
                    {
                        double downRightDiffSq = ColorDistanceSquared(rgb, p, below + 1);
                        solver.SetUndirectedEdgeCapacity(p, 2, (float)(diagonalWeight * Math.Exp(-beta * downRightDiffSq)));
                    }

                    if (x - 1 >= 0)
                    {
                        double downLeftDiffSq = ColorDistanceSquared(rgb, p, below - 1);
                        solver.SetUndirectedEdgeCapacity(p, 3, (float)(diagonalWeight * Math.Exp(-beta * downLeftDiffSq)));
                    }
                }
            }
        });
    }

    private static double ColorDistanceSquared(float[] rgb, int pixelA, int pixelB)
    {
        int offsetA = pixelA * 3;
        int offsetB = pixelB * 3;
        double dr = rgb[offsetA + 0] - rgb[offsetB + 0];
        double dg = rgb[offsetA + 1] - rgb[offsetB + 1];
        double db = rgb[offsetA + 2] - rgb[offsetB + 2];
        return (dr * dr) + (dg * dg) + (db * db);
    }
}

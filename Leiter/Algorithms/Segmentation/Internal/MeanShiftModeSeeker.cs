namespace Leiter.Algorithms.Segmentation.Internal;

using System;
using System.Threading.Tasks;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Performs parallel Mean-Shift mode seeking in 5D joint spatial-CIELAB feature space,
/// using circle-tightened direct scans, spatial-color lattice acceleration, and deterministic coarse-to-fine refinement.
/// </summary>
internal sealed class MeanShiftModeSeeker
{
    private const int CoarseGridMinSpatialRadius = 12;
    private const int CoarseGridMinImageDimension = 8;
    private const int StridedLatticeMinSpatialRadius = 20;
    private const double ModeAgreementColorToleranceFactor = 0.5;
    private const double SourceInterpolationColorToleranceFactor = 0.5;
    private const double CornerMatchColorToleranceFactor = 0.35;

    private readonly Lab32[] sourcePixels;
    private readonly int width;
    private readonly int height;
    private readonly int spatialRadius;
    private readonly double spatialRadiusSquared;
    private readonly double rangeRadiusSquared;
    private readonly double convergenceEpsilonSquared;
    private readonly int maxIterations;
    private readonly MeanShiftSpatialLattice? lattice;

    private MeanShiftModeSeeker(
        Lab32[] sourcePixels,
        int width,
        int height,
        int spatialRadius,
        double rangeRadiusSquared,
        double convergenceEpsilonSquared,
        int maxIterations,
        MeanShiftSpatialLattice? lattice)
    {
        this.sourcePixels = sourcePixels;
        this.width = width;
        this.height = height;
        this.spatialRadius = spatialRadius;
        spatialRadiusSquared = (double)spatialRadius * spatialRadius;
        this.rangeRadiusSquared = rangeRadiusSquared;
        this.convergenceEpsilonSquared = convergenceEpsilonSquared;
        this.maxIterations = maxIterations;
        this.lattice = lattice;
    }

    private readonly record struct CoarseAxisBracket(int Lower, int Upper, double Fraction, bool IsOnCoarseGrid);

    private readonly record struct CoarseCornerSample(Lab32 SourceColor, MeanShiftMode Mode, double BilinearWeight);

    private struct MatchingCornerAccumulator
    {
        private MeanShiftModeAccumulator modeAccumulator;
        private MeanShiftMode referenceMode;
        private bool allAgree;

        public static MatchingCornerAccumulator Create() => new() { allAgree = true };

        public void TryAdd(
            in CoarseCornerSample corner,
            Lab32 pixelColor,
            double cornerMatchToleranceSquared,
            double rangeRadiusSquared,
            double modeColorToleranceSquared,
            double spatialRadiusSquared)
        {
            if (!allAgree)
                return;

            if (pixelColor.EuclideanDistanceSquared(corner.SourceColor) > cornerMatchToleranceSquared ||
                pixelColor.EuclideanDistanceSquared(corner.Mode.Color) > rangeRadiusSquared)
            {
                return;
            }

            if (modeAccumulator.Count == 0)
            {
                referenceMode = corner.Mode;
            }
            else if (corner.Mode.Color.EuclideanDistanceSquared(referenceMode.Color) > modeColorToleranceSquared ||
                     corner.Mode.SpatialDistanceSquared(referenceMode) > spatialRadiusSquared)
            {
                allAgree = false;
                return;
            }

            modeAccumulator.AddWeighted(corner.Mode, corner.BilinearWeight);
        }

        public readonly bool TryBuildAverageMode(out MeanShiftMode mode)
        {
            if (modeAccumulator.Count < 2 || !allAgree)
            {
                mode = default;
                return false;
            }

            return modeAccumulator.TryGetMode(out mode);
        }
    }

    public static (MeanShiftMode[] Modes, Lab32[] SourcePixels) SeekModes(
        IReadOnlyMatrix<Lab32> labImage,
        MeanShiftOptions options,
        int latticeRadiusThreshold)
    {
        int width = labImage.Width;
        int height = labImage.Height;
        int pixelCount = width * height;
        Lab32[] sourcePixels = labImage.ToArray();

        var modes = new MeanShiftMode[pixelCount];
        if (pixelCount == 0)
            return (modes, sourcePixels);

        int spatialRadius = options.SpatialRadius;
        double rangeRadius = options.RangeRadius;
        int sampleStride = ResolveLatticeSampleStride(spatialRadius);

        MeanShiftSpatialLattice? lattice = spatialRadius > latticeRadiusThreshold
            ? MeanShiftSpatialLattice.Create(sourcePixels, width, height, spatialRadius, rangeRadius, sampleStride)
            : null;

        var seeker = new MeanShiftModeSeeker(
            sourcePixels,
            width,
            height,
            spatialRadius,
            rangeRadius * rangeRadius,
            options.ConvergenceEpsilon * options.ConvergenceEpsilon,
            options.MaxIterations,
            lattice);

        int coarseStride = ResolveCoarseGridStride(spatialRadius, width, height);
        if (coarseStride == 1)
        {
            seeker.SeekAllPixelsExact(modes);
        }
        else
        {
            seeker.SeekCoarseGrid(coarseStride, modes);
            seeker.RefineFinePixels(coarseStride, modes);
        }

        return (modes, sourcePixels);
    }

    private MeanShiftMode SeekPixelMode(int startX, int startY)
    {
        var currentMode = new MeanShiftMode(startX, startY, sourcePixels[(startY * width) + startX]);

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            MeanShiftMode nextMode = lattice is null
                ? ComputeDirectMeanStep(currentMode)
                : lattice.ComputeMeanStep(sourcePixels, currentMode, spatialRadiusSquared, rangeRadiusSquared);

            double stepDistanceSquared = currentMode.SpatialDistanceSquared(nextMode) +
                                         nextMode.Color.EuclideanDistanceSquared(currentMode.Color);
            currentMode = nextMode;
            if (stepDistanceSquared <= convergenceEpsilonSquared)
                break;
        }

        return currentMode;
    }

    private MeanShiftMode ComputeDirectMeanStep(MeanShiftMode currentMode)
    {
        int minY = Math.Max(0, (int)Math.Floor(currentMode.Y - spatialRadius));
        int maxY = Math.Min(height - 1, (int)Math.Ceiling(currentMode.Y + spatialRadius));

        var accumulator = default(MeanShiftModeAccumulator);

        for (int y = minY; y <= maxY; y++)
        {
            double deltaY = y - currentMode.Y;
            double deltaYSquared = deltaY * deltaY;
            if (deltaYSquared > spatialRadiusSquared)
                continue;

            double horizontalSpan = Math.Sqrt(spatialRadiusSquared - deltaYSquared);
            int rowMinX = Math.Max(0, (int)Math.Floor(currentMode.X - horizontalSpan));
            int rowMaxX = Math.Min(width - 1, (int)Math.Ceiling(currentMode.X + horizontalSpan));
            int rowOffset = y * width;

            for (int x = rowMinX; x <= rowMaxX; x++)
            {
                double deltaX = x - currentMode.X;
                double spatialDistanceSquared = (deltaX * deltaX) + deltaYSquared;
                if (spatialDistanceSquared > spatialRadiusSquared)
                    continue;

                Lab32 pixelColor = sourcePixels[rowOffset + x];
                if (pixelColor.EuclideanDistanceSquared(currentMode.Color) <= rangeRadiusSquared)
                {
                    accumulator.Add(x, y, pixelColor);
                }
            }
        }

        return accumulator.ToModeOrFallback(currentMode);
    }

    private static int ResolveLatticeSampleStride(int spatialRadius) =>
        spatialRadius >= StridedLatticeMinSpatialRadius
            ? Math.Clamp(spatialRadius / 10, 2, 4)
            : 1;

    private static int ResolveCoarseGridStride(int spatialRadius, int width, int height) =>
        (spatialRadius >= CoarseGridMinSpatialRadius &&
         width >= CoarseGridMinImageDimension &&
         height >= CoarseGridMinImageDimension)
            ? Math.Clamp(spatialRadius / 6, 2, 4)
            : 1;

    private void SeekAllPixelsExact(MeanShiftMode[] modes)
    {
        Parallel.For(0, height, y =>
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                modes[rowStart + x] = SeekPixelMode(x, y);
            }
        });
    }

    private void SeekCoarseGrid(int coarseStride, MeanShiftMode[] modes)
    {
        int[] coarseXs = BuildCoarseAxisCoordinates(width, coarseStride);
        int[] coarseYs = BuildCoarseAxisCoordinates(height, coarseStride);

        Parallel.For(0, coarseYs.Length, yi =>
        {
            int coarseY = coarseYs[yi];
            int rowStart = coarseY * width;
            for (int xi = 0; xi < coarseXs.Length; xi++)
            {
                int coarseX = coarseXs[xi];
                modes[rowStart + coarseX] = SeekPixelMode(coarseX, coarseY);
            }
        });
    }

    private void RefineFinePixels(int coarseStride, MeanShiftMode[] modes)
    {
        double modeColorToleranceSquared = (ModeAgreementColorToleranceFactor * ModeAgreementColorToleranceFactor) * rangeRadiusSquared;
        double sourceColorToleranceSquared = (SourceInterpolationColorToleranceFactor * SourceInterpolationColorToleranceFactor) * rangeRadiusSquared;
        double cornerMatchToleranceSquared = (CornerMatchColorToleranceFactor * CornerMatchColorToleranceFactor) * rangeRadiusSquared;

        Parallel.For(0, height, y =>
        {
            CoarseAxisBracket yBracket = ComputeCoarseBracket(y, height, coarseStride);
            int rowStart = y * width;

            for (int x = 0; x < width; x++)
            {
                CoarseAxisBracket xBracket = ComputeCoarseBracket(x, width, coarseStride);
                if (xBracket.IsOnCoarseGrid && yBracket.IsOnCoarseGrid)
                    continue;

                modes[rowStart + x] = TryInterpolatePixelMode(
                    modes,
                    x,
                    y,
                    in xBracket,
                    in yBracket,
                    modeColorToleranceSquared,
                    sourceColorToleranceSquared,
                    cornerMatchToleranceSquared,
                    out MeanShiftMode interpolatedMode)
                    ? interpolatedMode
                    : SeekPixelMode(x, y);
            }
        });
    }

    private static CoarseAxisBracket ComputeCoarseBracket(int coordinate, int axisLength, int coarseStride)
    {
        int lastIndex = axisLength - 1;
        if (coordinate == lastIndex)
            return new CoarseAxisBracket(coordinate, coordinate, 0.0, IsOnCoarseGrid: true);

        int lower = (coordinate / coarseStride) * coarseStride;
        int upper = Math.Min(lower + coarseStride, lastIndex);
        double fraction = upper > lower ? (double)(coordinate - lower) / (upper - lower) : 0.0;
        return new CoarseAxisBracket(lower, upper, fraction, IsOnCoarseGrid: coordinate == lower);
    }

    private bool TryInterpolatePixelMode(
        MeanShiftMode[] modes,
        int x,
        int y,
        in CoarseAxisBracket xBracket,
        in CoarseAxisBracket yBracket,
        double modeColorToleranceSquared,
        double sourceColorToleranceSquared,
        double cornerMatchToleranceSquared,
        out MeanShiftMode interpolatedMode)
    {
        int upperRowOffset = yBracket.Lower * width;
        int lowerRowOffset = yBracket.Upper * width;
        int index00 = upperRowOffset + xBracket.Lower;
        int index10 = upperRowOffset + xBracket.Upper;
        int index01 = lowerRowOffset + xBracket.Lower;
        int index11 = lowerRowOffset + xBracket.Upper;

        double oneMinusTx = 1.0 - xBracket.Fraction;
        double oneMinusTy = 1.0 - yBracket.Fraction;
        var corner00 = new CoarseCornerSample(sourcePixels[index00], modes[index00], oneMinusTx * oneMinusTy);
        var corner10 = new CoarseCornerSample(sourcePixels[index10], modes[index10], xBracket.Fraction * oneMinusTy);
        var corner01 = new CoarseCornerSample(sourcePixels[index01], modes[index01], oneMinusTx * yBracket.Fraction);
        var corner11 = new CoarseCornerSample(sourcePixels[index11], modes[index11], xBracket.Fraction * yBracket.Fraction);

        Lab32 pixelColor = sourcePixels[(y * width) + x];

        if (CoarseCornersAgree(
            corner00.Mode,
            corner10.Mode,
            corner01.Mode,
            corner11.Mode,
            modeColorToleranceSquared,
            spatialRadiusSquared))
        {
            return TryInterpolateAllFourCorners(
                pixelColor,
                in corner00,
                in corner10,
                in corner01,
                in corner11,
                sourceColorToleranceSquared,
                rangeRadiusSquared,
                out interpolatedMode);
        }

        if (!xBracket.IsOnCoarseGrid && !yBracket.IsOnCoarseGrid)
        {
            return TryInterpolateMatchingCornerSubset(
                pixelColor,
                in corner00,
                in corner10,
                in corner01,
                in corner11,
                cornerMatchToleranceSquared,
                rangeRadiusSquared,
                modeColorToleranceSquared,
                spatialRadiusSquared,
                out interpolatedMode);
        }

        interpolatedMode = default;
        return false;
    }

    private static bool CoarseCornersAgree(
        MeanShiftMode mode00,
        MeanShiftMode mode10,
        MeanShiftMode mode01,
        MeanShiftMode mode11,
        double modeColorToleranceSquared,
        double spatialRadiusSquared) =>
        mode00.Color.EuclideanDistanceSquared(mode10.Color) <= modeColorToleranceSquared &&
        mode00.Color.EuclideanDistanceSquared(mode01.Color) <= modeColorToleranceSquared &&
        mode00.Color.EuclideanDistanceSquared(mode11.Color) <= modeColorToleranceSquared &&
        mode00.SpatialDistanceSquared(mode10) <= spatialRadiusSquared &&
        mode00.SpatialDistanceSquared(mode01) <= spatialRadiusSquared &&
        mode00.SpatialDistanceSquared(mode11) <= spatialRadiusSquared;

    private static bool TryInterpolateAllFourCorners(
        Lab32 pixelColor,
        in CoarseCornerSample corner00,
        in CoarseCornerSample corner10,
        in CoarseCornerSample corner01,
        in CoarseCornerSample corner11,
        double sourceColorToleranceSquared,
        double rangeRadiusSquared,
        out MeanShiftMode interpolatedMode)
    {
        Lab32 interpolatedSource = BlendWeightedLab(
            corner00.SourceColor,
            corner10.SourceColor,
            corner01.SourceColor,
            corner11.SourceColor,
            corner00.BilinearWeight,
            corner10.BilinearWeight,
            corner01.BilinearWeight,
            corner11.BilinearWeight);

        if (pixelColor.EuclideanDistanceSquared(interpolatedSource) > sourceColorToleranceSquared ||
            pixelColor.EuclideanDistanceSquared(corner00.Mode.Color) > rangeRadiusSquared)
        {
            interpolatedMode = default;
            return false;
        }

        double interpolatedX =
            (corner00.BilinearWeight * corner00.Mode.X) +
            (corner10.BilinearWeight * corner10.Mode.X) +
            (corner01.BilinearWeight * corner01.Mode.X) +
            (corner11.BilinearWeight * corner11.Mode.X);
        double interpolatedY =
            (corner00.BilinearWeight * corner00.Mode.Y) +
            (corner10.BilinearWeight * corner10.Mode.Y) +
            (corner01.BilinearWeight * corner01.Mode.Y) +
            (corner11.BilinearWeight * corner11.Mode.Y);
        Lab32 interpolatedColor = BlendWeightedLab(
            corner00.Mode.Color,
            corner10.Mode.Color,
            corner01.Mode.Color,
            corner11.Mode.Color,
            corner00.BilinearWeight,
            corner10.BilinearWeight,
            corner01.BilinearWeight,
            corner11.BilinearWeight);

        interpolatedMode = new MeanShiftMode(interpolatedX, interpolatedY, interpolatedColor);
        return true;
    }

    private static bool TryInterpolateMatchingCornerSubset(
        Lab32 pixelColor,
        in CoarseCornerSample corner00,
        in CoarseCornerSample corner10,
        in CoarseCornerSample corner01,
        in CoarseCornerSample corner11,
        double cornerMatchToleranceSquared,
        double rangeRadiusSquared,
        double modeColorToleranceSquared,
        double spatialRadiusSquared,
        out MeanShiftMode interpolatedMode)
    {
        MatchingCornerAccumulator accumulator = MatchingCornerAccumulator.Create();
        accumulator.TryAdd(in corner00, pixelColor, cornerMatchToleranceSquared, rangeRadiusSquared, modeColorToleranceSquared, spatialRadiusSquared);
        accumulator.TryAdd(in corner10, pixelColor, cornerMatchToleranceSquared, rangeRadiusSquared, modeColorToleranceSquared, spatialRadiusSquared);
        accumulator.TryAdd(in corner01, pixelColor, cornerMatchToleranceSquared, rangeRadiusSquared, modeColorToleranceSquared, spatialRadiusSquared);
        accumulator.TryAdd(in corner11, pixelColor, cornerMatchToleranceSquared, rangeRadiusSquared, modeColorToleranceSquared, spatialRadiusSquared);
        return accumulator.TryBuildAverageMode(out interpolatedMode);
    }

    private static Lab32 BlendWeightedLab(
        Lab32 c00,
        Lab32 c10,
        Lab32 c01,
        Lab32 c11,
        double w00,
        double w10,
        double w01,
        double w11) =>
        new(
            (float)((w00 * c00.L) + (w10 * c10.L) + (w01 * c01.L) + (w11 * c11.L)),
            (float)((w00 * c00.A) + (w10 * c10.A) + (w01 * c01.A) + (w11 * c11.A)),
            (float)((w00 * c00.B) + (w10 * c10.B) + (w01 * c01.B) + (w11 * c11.B)));

    private static int[] BuildCoarseAxisCoordinates(int axisLength, int coarseStride)
    {
        int gridCount = ((axisLength - 1) / coarseStride) + 1;
        bool appendLastPixel = ((axisLength - 1) % coarseStride) != 0;
        var coordinates = new int[gridCount + (appendLastPixel ? 1 : 0)];

        for (int i = 0; i < gridCount; i++)
        {
            coordinates[i] = i * coarseStride;
        }

        if (appendLastPixel)
        {
            coordinates[^1] = axisLength - 1;
        }

        return coordinates;
    }
}

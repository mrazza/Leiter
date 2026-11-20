namespace Leiter.Algorithms.Segmentation.Internal;

using System;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Performs parallel Mean-Shift mode seeking in 5D joint spatial-CIELAB feature space,
/// using circle-tightened direct scans, spatial-color lattice acceleration, and deterministic coarse-to-fine refinement.
/// </summary>
internal sealed class MeanShiftModeSeeker
{
    internal const int LatticeRadiusThreshold = 16;

    private const int CoarseGridMinSpatialRadius = 12;
    private const int CoarseGridMinImageDimension = 8;
    private const int StridedLatticeMinSpatialRadius = 20;
    private const double ModeAgreementColorToleranceFactor = 0.5;
    private const double SourceInterpolationColorToleranceFactor = 0.5;
    private const double CornerMatchColorToleranceFactor = 0.35;

    private readonly Lab32[] sourcePixels;
    private readonly Size size;
    private readonly int spatialRadius;
    private readonly double spatialRadiusSquared;
    private readonly double rangeRadiusSquared;
    private readonly double convergenceEpsilonSquared;
    private readonly int maxIterations;
    private readonly MeanShiftSpatialLattice? lattice;

    private MeanShiftModeSeeker(
        Lab32[] sourcePixels,
        Size size,
        int spatialRadius,
        double rangeRadiusSquared,
        double convergenceEpsilonSquared,
        int maxIterations,
        MeanShiftSpatialLattice? lattice)
    {
        this.sourcePixels = sourcePixels;
        this.size = size;
        this.spatialRadius = spatialRadius;
        spatialRadiusSquared = (double)spatialRadius * spatialRadius;
        this.rangeRadiusSquared = rangeRadiusSquared;
        this.convergenceEpsilonSquared = convergenceEpsilonSquared;
        this.maxIterations = maxIterations;
        this.lattice = lattice;
    }

    internal readonly record struct CoarseCornerSample(Lab32 SourceColor, MeanShiftMode Mode, double BilinearWeight);

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
        int latticeRadiusThreshold = LatticeRadiusThreshold,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(labImage);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        Size size = labImage.Size;
        int pixelCount = size.Count;
        Lab32[] sourcePixels = labImage.ToArray();

        var modes = new MeanShiftMode[pixelCount];
        if (pixelCount == 0)
            return (modes, sourcePixels);

        int spatialRadius = options.SpatialRadius;
        double rangeRadius = options.RangeRadius;
        int sampleStride = ResolveLatticeSampleStride(spatialRadius);

        MeanShiftSpatialLattice? lattice = spatialRadius > latticeRadiusThreshold
            ? MeanShiftSpatialLattice.Create(sourcePixels, size.Width, size.Height, spatialRadius, rangeRadius, sampleStride, cancellationToken)
            : null;

        var seeker = new MeanShiftModeSeeker(
            sourcePixels,
            size,
            spatialRadius,
            rangeRadius * rangeRadius,
            options.ConvergenceEpsilon * options.ConvergenceEpsilon,
            options.MaxIterations,
            lattice);

        int coarseStride = ResolveCoarseGridStride(spatialRadius, size.Width, size.Height);
        if (coarseStride == 1)
        {
            seeker.SeekAllPixelsExact(modes, cancellationToken);
        }
        else
        {
            int[] coarseXs = BuildCoarseAxisCoordinates(size.Width, coarseStride);
            int[] coarseYs = BuildCoarseAxisCoordinates(size.Height, coarseStride);
            seeker.SeekCoarseGrid(coarseXs, coarseYs, modes, cancellationToken);
            seeker.RefineFinePixels(coarseStride, coarseXs, coarseYs, modes, cancellationToken);
        }

        return (modes, sourcePixels);
    }

    private MeanShiftMode SeekPixelMode(int startX, int startY)
    {
        var currentMode = new MeanShiftMode(startX, startY, sourcePixels[size.IndexOf(startX, startY)]);

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            MeanShiftMode nextMode = lattice is null
                ? ComputeDirectMeanStep(currentMode)
                : lattice.ComputeMeanStep(currentMode, spatialRadiusSquared, rangeRadiusSquared);

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
        (int minX, int maxX, int minY, int maxY) = size.ClampWindow(
            (int)Math.Floor(currentMode.X),
            (int)Math.Floor(currentMode.Y),
            spatialRadius);

        var accumulator = default(MeanShiftModeAccumulator);

        for (int y = minY; y <= maxY; y++)
        {
            double deltaY = y - currentMode.Y;
            double deltaYSquared = deltaY * deltaY;
            if (deltaYSquared > spatialRadiusSquared)
                continue;

            double horizontalSpan = Math.Sqrt(spatialRadiusSquared - deltaYSquared);
            int rowMinX = Math.Max(minX, (int)Math.Floor(currentMode.X - horizontalSpan));
            int rowMaxX = Math.Min(maxX, (int)Math.Ceiling(currentMode.X + horizontalSpan));
            int rowOffset = size.IndexOf(0, y);

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

    private void SeekAllPixelsExact(MeanShiftMode[] modes, CancellationToken cancellationToken)
    {
        int width = size.Width;
        int height = size.Height;

        void SeekRow(int y)
        {
            int rowStart = size.IndexOf(0, y);
            for (int x = 0; x < width; x++)
            {
                modes[rowStart + x] = SeekPixelMode(x, y);
            }
        }

        if (cancellationToken.CanBeCanceled)
        {
            Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, SeekRow);
        }
        else
        {
            Parallel.For(0, height, SeekRow);
        }
    }

    private void SeekCoarseGrid(int[] coarseXs, int[] coarseYs, MeanShiftMode[] modes, CancellationToken cancellationToken)
    {
        void SeekCoarseRow(int yi)
        {
            int coarseY = coarseYs[yi];
            int rowStart = size.IndexOf(0, coarseY);
            for (int xi = 0; xi < coarseXs.Length; xi++)
            {
                int coarseX = coarseXs[xi];
                modes[rowStart + coarseX] = SeekPixelMode(coarseX, coarseY);
            }
        }

        if (cancellationToken.CanBeCanceled)
        {
            Parallel.For(0, coarseYs.Length, new ParallelOptions { CancellationToken = cancellationToken }, SeekCoarseRow);
        }
        else
        {
            Parallel.For(0, coarseYs.Length, SeekCoarseRow);
        }
    }

    private void RefineFinePixels(
        int coarseStride,
        int[] coarseXs,
        int[] coarseYs,
        MeanShiftMode[] modes,
        CancellationToken cancellationToken)
    {
        int width = size.Width;
        int height = size.Height;
        double modeColorToleranceSquared = (ModeAgreementColorToleranceFactor * ModeAgreementColorToleranceFactor) * rangeRadiusSquared;
        double sourceColorToleranceSquared = (SourceInterpolationColorToleranceFactor * SourceInterpolationColorToleranceFactor) * rangeRadiusSquared;
        double cornerMatchToleranceSquared = (CornerMatchColorToleranceFactor * CornerMatchColorToleranceFactor) * rangeRadiusSquared;

        var xAxes = new MatrixResampler.BilinearSampleAxis[width];
        for (int x = 0; x < width; x++)
        {
            xAxes[x] = ComputeCoarseBilinearAxis(x, coarseXs, coarseStride, width);
        }

        void RefineRow(int y)
        {
            MatrixResampler.BilinearSampleAxis yAxis = ComputeCoarseBilinearAxis(y, coarseYs, coarseStride, height);
            bool isOnCoarseY = y == yAxis.LowerIndex;
            int rowStart = size.IndexOf(0, y);

            for (int x = 0; x < width; x++)
            {
                MatrixResampler.BilinearSampleAxis xAxis = xAxes[x];
                bool isOnCoarseX = x == xAxis.LowerIndex;
                if (isOnCoarseX && isOnCoarseY)
                    continue;

                modes[rowStart + x] = TryInterpolatePixelMode(
                    modes,
                    x,
                    y,
                    in xAxis,
                    in yAxis,
                    isOnCoarseX,
                    isOnCoarseY,
                    modeColorToleranceSquared,
                    sourceColorToleranceSquared,
                    cornerMatchToleranceSquared,
                    out MeanShiftMode interpolatedMode)
                    ? interpolatedMode
                    : SeekPixelMode(x, y);
            }
        }

        if (cancellationToken.CanBeCanceled)
        {
            Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, RefineRow);
        }
        else
        {
            Parallel.For(0, height, RefineRow);
        }
    }

    private static MatrixResampler.BilinearSampleAxis ComputeCoarseBilinearAxis(
        int coordinate,
        int[] coarseCoordinates,
        int coarseStride,
        int axisLength)
    {
        int lastIndex = axisLength - 1;
        double coarseCoord;
        if (coordinate >= lastIndex)
        {
            coarseCoord = coarseCoordinates.Length - 1;
        }
        else
        {
            int lowerGridIndex = coordinate / coarseStride;
            int lower = coarseCoordinates[lowerGridIndex];
            int upper = coarseCoordinates[lowerGridIndex + 1];
            coarseCoord = lowerGridIndex + ((double)(coordinate - lower) / (upper - lower));
        }

        MatrixResampler.BilinearSampleAxis gridAxis = MatrixResampler.ComputeBilinearSampleAxis(
            coarseCoord,
            coarseCoordinates.Length);

        return new MatrixResampler.BilinearSampleAxis(
            coarseCoordinates[gridAxis.LowerIndex],
            coarseCoordinates[gridAxis.UpperIndex],
            gridAxis.LowerWeight,
            gridAxis.UpperWeight);
    }

    private bool TryInterpolatePixelMode(
        MeanShiftMode[] modes,
        int x,
        int y,
        in MatrixResampler.BilinearSampleAxis xAxis,
        in MatrixResampler.BilinearSampleAxis yAxis,
        bool isOnCoarseX,
        bool isOnCoarseY,
        double modeColorToleranceSquared,
        double sourceColorToleranceSquared,
        double cornerMatchToleranceSquared,
        out MeanShiftMode interpolatedMode)
    {
        int upperRowOffset = size.IndexOf(0, yAxis.LowerIndex);
        int lowerRowOffset = size.IndexOf(0, yAxis.UpperIndex);
        int index00 = upperRowOffset + xAxis.LowerIndex;
        int index10 = upperRowOffset + xAxis.UpperIndex;
        int index01 = lowerRowOffset + xAxis.LowerIndex;
        int index11 = lowerRowOffset + xAxis.UpperIndex;

        var corner00 = new CoarseCornerSample(sourcePixels[index00], modes[index00], xAxis.LowerWeight * yAxis.LowerWeight);
        var corner10 = new CoarseCornerSample(sourcePixels[index10], modes[index10], xAxis.UpperWeight * yAxis.LowerWeight);
        var corner01 = new CoarseCornerSample(sourcePixels[index01], modes[index01], xAxis.LowerWeight * yAxis.UpperWeight);
        var corner11 = new CoarseCornerSample(sourcePixels[index11], modes[index11], xAxis.UpperWeight * yAxis.UpperWeight);

        Lab32 pixelColor = sourcePixels[size.IndexOf(x, y)];

        if (CoarseCornersAgree(
            in corner00,
            in corner10,
            in corner01,
            in corner11,
            modeColorToleranceSquared,
            spatialRadiusSquared,
            out MeanShiftMode referenceMode))
        {
            return TryInterpolateAllFourCorners(
                pixelColor,
                referenceMode,
                in corner00,
                in corner10,
                in corner01,
                in corner11,
                sourceColorToleranceSquared,
                rangeRadiusSquared,
                out interpolatedMode);
        }

        if (!isOnCoarseX && !isOnCoarseY)
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

    internal static bool CoarseCornersAgree(
        in CoarseCornerSample corner00,
        in CoarseCornerSample corner10,
        in CoarseCornerSample corner01,
        in CoarseCornerSample corner11,
        double modeColorToleranceSquared,
        double spatialRadiusSquared,
        out MeanShiftMode referenceMode)
    {
        referenceMode = default;
        bool hasReference = false;

        return CheckCornerAgreement(in corner00, ref hasReference, ref referenceMode, modeColorToleranceSquared, spatialRadiusSquared) &&
               CheckCornerAgreement(in corner10, ref hasReference, ref referenceMode, modeColorToleranceSquared, spatialRadiusSquared) &&
               CheckCornerAgreement(in corner01, ref hasReference, ref referenceMode, modeColorToleranceSquared, spatialRadiusSquared) &&
               CheckCornerAgreement(in corner11, ref hasReference, ref referenceMode, modeColorToleranceSquared, spatialRadiusSquared) &&
               hasReference;
    }

    private static bool CheckCornerAgreement(
        in CoarseCornerSample corner,
        ref bool hasReference,
        ref MeanShiftMode referenceMode,
        double modeColorToleranceSquared,
        double spatialRadiusSquared)
    {
        if (corner.BilinearWeight <= 0.0)
            return true;

        if (!hasReference)
        {
            referenceMode = corner.Mode;
            hasReference = true;
            return true;
        }

        return corner.Mode.Color.EuclideanDistanceSquared(referenceMode.Color) <= modeColorToleranceSquared &&
               corner.Mode.SpatialDistanceSquared(referenceMode) <= spatialRadiusSquared;
    }

    internal static bool TryInterpolateAllFourCorners(
        Lab32 pixelColor,
        MeanShiftMode referenceMode,
        in CoarseCornerSample corner00,
        in CoarseCornerSample corner10,
        in CoarseCornerSample corner01,
        in CoarseCornerSample corner11,
        double sourceColorToleranceSquared,
        double rangeRadiusSquared,
        out MeanShiftMode interpolatedMode)
    {
        var sourceAccumulator = default(MeanShiftModeAccumulator);
        var modeAccumulator = default(MeanShiftModeAccumulator);

        AccumulatePositiveWeightCorner(in corner00, ref sourceAccumulator, ref modeAccumulator);
        AccumulatePositiveWeightCorner(in corner10, ref sourceAccumulator, ref modeAccumulator);
        AccumulatePositiveWeightCorner(in corner01, ref sourceAccumulator, ref modeAccumulator);
        AccumulatePositiveWeightCorner(in corner11, ref sourceAccumulator, ref modeAccumulator);

        if (!sourceAccumulator.TryGetMode(out MeanShiftMode interpolatedSource) ||
            pixelColor.EuclideanDistanceSquared(interpolatedSource.Color) > sourceColorToleranceSquared ||
            pixelColor.EuclideanDistanceSquared(referenceMode.Color) > rangeRadiusSquared)
        {
            interpolatedMode = default;
            return false;
        }

        return modeAccumulator.TryGetMode(out interpolatedMode);
    }

    private static void AccumulatePositiveWeightCorner(
        in CoarseCornerSample corner,
        ref MeanShiftModeAccumulator sourceAccumulator,
        ref MeanShiftModeAccumulator modeAccumulator)
    {
        if (corner.BilinearWeight <= 0.0)
            return;

        sourceAccumulator.AddWeighted(new MeanShiftMode(0.0, 0.0, corner.SourceColor), corner.BilinearWeight);
        modeAccumulator.AddWeighted(corner.Mode, corner.BilinearWeight);
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

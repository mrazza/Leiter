namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using System;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MeanShiftSpatialLattice"/>.
/// </summary>
public class MeanShiftSpatialLatticeTests
{
    [Fact]
    public void ComputeMeanStep_AcrossVariedCoordinates_ShouldMatchDirectBruteForceScan()
    {
        const int width = 24;
        const int height = 24;
        const double spatialRadius = 6.0;
        const double rangeRadius = 15.0;
        double spatialRadiusSq = spatialRadius * spatialRadius;
        double rangeRadiusSq = rangeRadius * rangeRadius;

        var pixels = new Lab32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Varied spatial and chromatic gradient
                float l = 20.0f + ((x + y) * 1.5f);
                float a = -20.0f + (x * 1.8f);
                float b = 30.0f - (y * 2.0f);
                pixels[(y * width) + x] = new Lab32(l, a, b);
            }
        }

        var lattice = new MeanShiftSpatialLattice(pixels, width, height, spatialRadius, rangeRadius);

        MeanShiftMode[] testModes =
        [
            // Center integer coordinate
            new(12.0, 12.0, pixels[(12 * width) + 12]),
            // Fractional coordinate inside bounds
            new(7.35, 15.65, new Lab32(45.0f, 5.0f, -10.0f)),
            // Top-left corner
            new(0.0, 0.0, pixels[0]),
            // Bottom-right corner
            new(width - 1, height - 1, pixels[(width * height) - 1]),
            // Fractional border coordinate
            new(0.5, 0.5, pixels[0]),
            // Negative coordinates (window partially overlaps image)
            new(-2.0, -1.5, new Lab32(25.0f, -15.0f, 25.0f)),
            // Outside right/bottom boundary
            new(width + 1.5, height + 1.0, new Lab32(80.0f, 15.0f, -15.0f)),
        ];

        foreach (MeanShiftMode query in testModes)
        {
            MeanShiftMode directExpected = DirectBruteForceScan(pixels, width, height, query, spatialRadiusSq, rangeRadiusSq);
            MeanShiftMode latticeActual = lattice.ComputeMeanStep(query, spatialRadiusSq, rangeRadiusSq);

            Assert.Equal(directExpected.X, latticeActual.X, 4);
            Assert.Equal(directExpected.Y, latticeActual.Y, 4);
            Assert.Equal(directExpected.Color.L, latticeActual.Color.L, 3);
            Assert.Equal(directExpected.Color.A, latticeActual.Color.A, 3);
            Assert.Equal(directExpected.Color.B, latticeActual.Color.B, 3);
        }
    }

    [Fact]
    public void ComputeMeanStep_WhenNoPixelsInRange_ShouldReturnCurrentUnchanged()
    {
        const int width = 10;
        const int height = 10;
        const double spatialRadius = 5.0;
        const double rangeRadius = 2.0;

        var pixels = new Lab32[width * height];
        Array.Fill(pixels, new Lab32(10.0f, 0.0f, 0.0f));

        var lattice = new MeanShiftSpatialLattice(pixels, width, height, spatialRadius, rangeRadius);

        // Color is far outside the pixel colors
        var query = new MeanShiftMode(5.0, 5.0, new Lab32(90.0f, 50.0f, -50.0f));
        MeanShiftMode result = lattice.ComputeMeanStep(query, spatialRadius * spatialRadius, rangeRadius * rangeRadius);

        Assert.Equal(query.X, result.X);
        Assert.Equal(query.Y, result.Y);
        Assert.Equal(query.Color, result.Color);
    }

    [Fact]
    public void ComputeMeanStep_EmptyLattice_ShouldReturnCurrentUnchanged()
    {
        var lattice = new MeanShiftSpatialLattice([], 0, 0, 5.0, 5.0);
        var query = new MeanShiftMode(2.0, 3.0, new Lab32(50.0f, 0.0f, 0.0f));

        MeanShiftMode result = lattice.ComputeMeanStep(query, 25.0, 25.0);

        Assert.Equal(query.X, result.X);
        Assert.Equal(query.Y, result.Y);
        Assert.Equal(query.Color, result.Color);
    }

    [Fact]
    public void Constructor_InvalidArguments_ShouldThrowArgumentOutOfRangeException()
    {
        Lab32[] sample = [new(50f, 0f, 0f)];

        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftSpatialLattice(sample, -1, 1, 5.0, 5.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftSpatialLattice(sample, 1, -1, 5.0, 5.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftSpatialLattice(sample, 1, 1, 0.0, 5.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftSpatialLattice(sample, 1, 1, -1.0, 5.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftSpatialLattice(sample, 1, 1, double.NaN, 5.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftSpatialLattice(sample, 1, 1, 5.0, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftSpatialLattice(sample, 1, 1, 5.0, -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftSpatialLattice(sample, 1, 1, 5.0, double.NaN));
    }

    private static MeanShiftMode DirectBruteForceScan(
        ReadOnlySpan<Lab32> pixels,
        int width,
        int height,
        MeanShiftMode current,
        double spatialRadiusSq,
        double rangeRadiusSq)
    {
        double sumX = 0;
        double sumY = 0;
        double sumL = 0;
        double sumA = 0;
        double sumB = 0;
        int count = 0;

        for (int y = 0; y < height; y++)
        {
            double dy = y - current.Y;
            double dySq = dy * dy;
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                double dx = x - current.X;
                double distSqSpatial = (dx * dx) + dySq;
                if (distSqSpatial <= spatialRadiusSq)
                {
                    Lab32 pixelColor = pixels[rowOffset + x];
                    if (pixelColor.EuclideanDistanceSquared(current.Color) <= rangeRadiusSq)
                    {
                        sumX += x;
                        sumY += y;
                        sumL += pixelColor.L;
                        sumA += pixelColor.A;
                        sumB += pixelColor.B;
                        count++;
                    }
                }
            }
        }

        if (count == 0)
            return current;

        double invCount = 1.0 / count;
        return new MeanShiftMode(
            sumX * invCount,
            sumY * invCount,
            new Lab32((float)(sumL * invCount), (float)(sumA * invCount), (float)(sumB * invCount)));
    }
}

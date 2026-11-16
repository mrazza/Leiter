namespace Leiter.Tests.Algorithms.DistanceTransform;

using System;
using System.Collections.Generic;
using Leiter.Algorithms.DistanceTransform;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="JumpFlooding"/>.
/// </summary>
public class JumpFloodingTests
{
    [Fact]
    public void ExtractBoundarySeeds_ShouldDifferentiate4And8ConnectedBackground()
    {
        var mask = new MaskMatrix(5, 5);
        for (int y = 1; y <= 3; y++)
        {
            for (int x = 1; x <= 3; x++)
            {
                mask[x, y] = true;
            }
        }

        mask[3, 3] = false;

        IReadOnlyList<Coord> eightConnected = JumpFlooding.ExtractBoundarySeeds(mask, eightConnectedBackground: true);
        IReadOnlyList<Coord> fourConnected = JumpFlooding.ExtractBoundarySeeds(mask, eightConnectedBackground: false);

        Assert.Contains(new Coord(2, 2), eightConnected);
        Assert.DoesNotContain(new Coord(2, 2), fourConnected);

        var emptyMask = new MaskMatrix(0, 0);
        Assert.Empty(JumpFlooding.ExtractBoundarySeeds(emptyMask));

        var allForeground = new MaskMatrix(3, 3);
        allForeground.SetAll(true);
        Assert.Empty(JumpFlooding.ExtractBoundarySeeds(allForeground));

        Assert.Throws<ArgumentNullException>(() => JumpFlooding.ExtractBoundarySeeds(null!));
    }

    [Fact]
    public void ComputeDistanceTransform_MultiSeedGrid_ShouldMatchExactEuclideanDistance()
    {
        var size = new Size(33, 29);
        Coord[] seeds =
        [
            new(3, 5),
            new(20, 4),
            new(14, 15),
            new(28, 24),
            new(5, 22),
            new(99, 99),
            new(-1, 3),
        ];

        Matrix<NearestSeed> nearestSeeds = JumpFlooding.ComputeNearestSeeds(size, seeds);
        Matrix<DoublePixel> distances = JumpFlooding.ComputeDistanceTransform(size, seeds);

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                double expectedMinDistSq = double.PositiveInfinity;
                foreach (Coord seed in seeds)
                {
                    if ((uint)seed.X >= (uint)size.Width || (uint)seed.Y >= (uint)size.Height)
                    {
                        continue;
                    }

                    double dx = x - seed.X;
                    double dy = y - seed.Y;
                    double distSq = (dx * dx) + (dy * dy);
                    if (distSq < expectedMinDistSq)
                    {
                        expectedMinDistSq = distSq;
                    }
                }

                double expectedDist = Math.Sqrt(expectedMinDistSq);
                Assert.Equal(expectedDist, distances[x, y].Value, 1e-9);

                NearestSeed entry = nearestSeeds[x, y];
                Assert.True(entry.HasSeed);
                Assert.Equal(expectedDist, entry.Seed.Distance(new Coord(x, y)), 1e-9);
            }
        }
    }

    [Fact]
    public void ComputeNearestSeeds_FromSeedMask_1x1_And_EmptyOrNoSeeds_ShouldWork()
    {
        var mask = new MaskMatrix(8, 6);
        mask[1, 1] = true;
        mask[6, 4] = true;

        Matrix<DoublePixel> dt = JumpFlooding.ComputeDistanceTransform(mask);
        Assert.Equal(0.0, dt[1, 1].Value, 1e-9);
        Assert.Equal(0.0, dt[6, 4].Value, 1e-9);
        Assert.Equal(Math.Sqrt(2.0), dt[2, 2].Value, 1e-9);

        // Symmetric tie break picks lower row-major seed index deterministically.
        Matrix<NearestSeed> tieSeeds = JumpFlooding.ComputeNearestSeeds(
            new Size(5, 1),
            [new Coord(4, 0), new Coord(0, 0)]);
        Assert.Equal(new Coord(0, 0), tieSeeds[2, 0].Seed);

        // 1x1 grid with seed.
        Matrix<NearestSeed> oneByOne = JumpFlooding.ComputeNearestSeeds(new Size(1, 1), [new Coord(0, 0)]);
        Assert.Equal(NearestSeed.FromCoord(new Coord(0, 0)), oneByOne[0, 0]);

        // No seeds -> NearestSeed.None and +Infinity distances.
        Matrix<NearestSeed> noSeeds = JumpFlooding.ComputeNearestSeeds(new Size(4, 3), []);
        for (int i = 0; i < noSeeds.Count; i++)
        {
            Assert.False(noSeeds[i].HasSeed);
            Assert.Equal(NearestSeed.None, noSeeds[i]);
        }

        Matrix<DoublePixel> noSeedDistances = JumpFlooding.ComputeDistanceTransform(new MaskMatrix(4, 3));
        for (int i = 0; i < noSeedDistances.Count; i++)
        {
            Assert.True(double.IsPositiveInfinity(noSeedDistances[i].Value));
        }

        // Empty matrices.
        var emptySize = new Size(0, 0);
        Assert.Empty(JumpFlooding.ComputeNearestSeeds(emptySize, []));
        Assert.Empty(JumpFlooding.ComputeDistanceTransform(new Size(3, 0), []));
        Assert.Empty(JumpFlooding.ComputeNearestSeeds(new MaskMatrix(0, 0)));
        Assert.Empty(JumpFlooding.ComputeDistanceTransform(new MaskMatrix(0, 0)));

        // Argument validation.
        Assert.Throws<ArgumentOutOfRangeException>(() => JumpFlooding.ComputeNearestSeeds(new Size(-1, 3), []));
        Assert.Throws<ArgumentOutOfRangeException>(() => JumpFlooding.ComputeNearestSeeds(new Size(3, -1), []));
        Assert.Throws<ArgumentNullException>(() => JumpFlooding.ComputeNearestSeeds(new Size(3, 3), null!));
        Assert.Throws<ArgumentNullException>(() => JumpFlooding.ComputeNearestSeeds(null!));
    }
}

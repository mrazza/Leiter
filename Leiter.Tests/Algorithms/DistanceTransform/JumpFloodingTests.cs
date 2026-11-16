namespace Leiter.Tests.Algorithms.DistanceTransform;

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
        // A 5x5 grid with foreground block [1..3] x [1..3] minus corner (3,3) is tested;
        // also place an internal foreground pixel (2,2) diagonally adjacent to background hole (3,3).
        var mask = new SequentialMatrix<LongPixel>(5, 5);
        for (int y = 1; y <= 3; y++)
        {
            for (int x = 1; x <= 3; x++)
                mask[x, y] = 1L;
        }
        mask[3, 3] = 0L;
        // Center pixel (2,2) has 4-connected neighbors (3,2),(1,2),(2,3),(2,1) all foreground,
        // and diagonal neighbor (3,3) = 0.

        IReadOnlyList<Coord> eightConnected = JumpFlooding.ExtractBoundarySeeds(mask, eightConnectedBackground: true);
        IReadOnlyList<Coord> fourConnected = JumpFlooding.ExtractBoundarySeeds(mask, eightConnectedBackground: false);

        Assert.Contains(new Coord(2, 2), eightConnected);
        Assert.DoesNotContain(new Coord(2, 2), fourConnected);

        var emptyMask = new SequentialMatrix<LongPixel>(0, 0);
        Assert.Empty(JumpFlooding.ExtractBoundarySeeds(emptyMask));

        var allForeground = new SequentialMatrix<LongPixel>(3, 3);
        allForeground.SetAll(1L);
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
            new(99, 99), // out-of-bounds seed must be ignored cleanly
            new(-1, 3),  // out-of-bounds seed must be ignored cleanly
        ];

        Matrix<LongPixel> seedIndices = JumpFlooding.ComputeNearestSeedIndices(size, seeds);
        Matrix<DoublePixel> distances = JumpFlooding.ComputeDistanceTransform(size, seeds);

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                double expectedMinDistSq = double.PositiveInfinity;
                foreach (Coord seed in seeds)
                {
                    if ((uint)seed.X >= (uint)size.Width || (uint)seed.Y >= (uint)size.Height)
                        continue;

                    double dx = x - seed.X;
                    double dy = y - seed.Y;
                    double distSq = (dx * dx) + (dy * dy);
                    if (distSq < expectedMinDistSq)
                        expectedMinDistSq = distSq;
                }

                double expectedDist = Math.Sqrt(expectedMinDistSq);
                Assert.Equal(expectedDist, distances[x, y].Value, 1e-9);

                long resolvedIdx = seedIndices[x, y].Value;
                Coord resolved = seedIndices.CoordFromIndex((int)resolvedIdx);
                Assert.Equal(expectedDist, resolved.Distance(new Coord(x, y)), 1e-9);
            }
        }
    }

    [Fact]
    public void ComputeNearestSeedIndices_FromSeedMask_1x1_And_EmptyOrNoSeeds_ShouldWork()
    {
        var mask = new SequentialMatrix<LongPixel>(8, 6);
        mask[1, 1] = 1L;
        mask[6, 4] = 2L;

        Matrix<DoublePixel> dt = JumpFlooding.ComputeDistanceTransform(mask);
        Assert.Equal(0.0, dt[1, 1].Value, 1e-9);
        Assert.Equal(0.0, dt[6, 4].Value, 1e-9);
        Assert.Equal(Math.Sqrt(2.0), dt[2, 2].Value, 1e-9);

        // Symmetric tie break picks lower row-major seed index deterministically.
        Matrix<LongPixel> tieIndices = JumpFlooding.ComputeNearestSeedIndices(
            new Size(5, 1),
            [new Coord(4, 0), new Coord(0, 0)]);
        Assert.Equal(0L, tieIndices[2, 0].Value);

        // 1x1 grid with seed
        Matrix<LongPixel> oneByOne = JumpFlooding.ComputeNearestSeedIndices(new Size(1, 1), [new Coord(0, 0)]);
        Assert.Equal(0L, oneByOne[0, 0].Value);

        // No seeds -> -1 indices and +Infinity distances
        Matrix<LongPixel> noSeedIndices = JumpFlooding.ComputeNearestSeedIndices(new Size(4, 3), []);
        for (int i = 0; i < noSeedIndices.Count; i++)
            Assert.Equal(-1L, noSeedIndices[i].Value);

        Matrix<DoublePixel> noSeedDistances = JumpFlooding.ComputeDistanceTransform(new SequentialMatrix<LongPixel>(4, 3));
        for (int i = 0; i < noSeedDistances.Count; i++)
            Assert.True(double.IsPositiveInfinity(noSeedDistances[i].Value));

        // Empty matrices
        var emptySize = new Size(0, 0);
        Assert.Empty(JumpFlooding.ComputeNearestSeedIndices(emptySize, []));
        Assert.Empty(JumpFlooding.ComputeDistanceTransform(new Size(3, 0), []));
        Assert.Empty(JumpFlooding.ComputeNearestSeedIndices(new SequentialMatrix<LongPixel>(0, 0)));
        Assert.Empty(JumpFlooding.ComputeDistanceTransform(new SequentialMatrix<LongPixel>(0, 0)));

        // Argument validation
        Assert.Throws<ArgumentOutOfRangeException>(() => JumpFlooding.ComputeNearestSeedIndices(new Size(-1, 3), []));
        Assert.Throws<ArgumentOutOfRangeException>(() => JumpFlooding.ComputeNearestSeedIndices(new Size(3, -1), []));
        Assert.Throws<ArgumentNullException>(() => JumpFlooding.ComputeNearestSeedIndices(new Size(3, 3), null!));
        Assert.Throws<ArgumentNullException>(() => JumpFlooding.ComputeNearestSeedIndices(null!));
    }
}

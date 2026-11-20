namespace Leiter.Tests.Algorithms.DistanceTransform;

using System;
using System.Collections.Generic;
using System.Threading;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.DistanceTransform;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="JumpFlooding"/>.
/// </summary>
public class JumpFloodingTests
{
    [Fact]
    public void ExtractBoundarySeeds_ShouldDifferentiate4And8ConnectedBackground()
    {
        SequentialMatrix<bool> mask = TestMatrices.CenteredBox(5, 5, 1, 1, 3, 3, true, false);
        mask[3, 3] = false;

        IReadOnlyList<Coord> eightConnected = JumpFlooding.ExtractBoundarySeeds(mask, backgroundConnectivity: GridConnectivity.EightConnected);
        IReadOnlyList<Coord> fourConnected = JumpFlooding.ExtractBoundarySeeds(mask, backgroundConnectivity: GridConnectivity.FourConnected);

        Assert.Contains(new Coord(2, 2), eightConnected);
        Assert.DoesNotContain(new Coord(2, 2), fourConnected);

        var emptyMask = new MaskMatrix(0, 0);
        Assert.Empty(JumpFlooding.ExtractBoundarySeeds(emptyMask));

        SequentialMatrix<bool> allForeground = TestMatrices.Constant(3, 3, true);
        Assert.Empty(JumpFlooding.ExtractBoundarySeeds(allForeground));

        Assert.Throws<ArgumentNullException>(() => JumpFlooding.ExtractBoundarySeeds(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => JumpFlooding.ExtractBoundarySeeds(mask, (GridConnectivity)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => JumpFlooding.ExtractBoundarySeeds(emptyMask, (GridConnectivity)99));
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
        Assert.False(NearestSeed.None.HasSeed);
        MatrixAssert.Equal(TestMatrices.Constant(4, 3, NearestSeed.None), noSeeds);

        Matrix<DoublePixel> noSeedDistances = JumpFlooding.ComputeDistanceTransform(new MaskMatrix(4, 3));
        MatrixAssert.Equal(TestMatrices.Constant(4, 3, new DoublePixel(double.PositiveInfinity)), noSeedDistances);

        // Empty matrices.
        var emptySize = new Size(0, 0);
        Assert.Empty(JumpFlooding.ComputeNearestSeeds(emptySize, []));
        Assert.Empty(JumpFlooding.ComputeDistanceTransform(new Size(3, 0), []));
        Assert.Empty(JumpFlooding.ComputeNearestSeeds(new MaskMatrix(0, 0)));
        Assert.Empty(JumpFlooding.ComputeDistanceTransform(new MaskMatrix(0, 0)));

        // Argument validation.
        ArgumentOutOfRangeException negW = Assert.Throws<ArgumentOutOfRangeException>(() => JumpFlooding.ComputeNearestSeeds(new Size(-1, 3), []));
        Assert.Equal("size", negW.ParamName);
        ArgumentOutOfRangeException negH = Assert.Throws<ArgumentOutOfRangeException>(() => JumpFlooding.ComputeNearestSeeds(new Size(3, -1), []));
        Assert.Equal("size", negH.ParamName);
        Assert.Throws<ArgumentNullException>(() => JumpFlooding.ComputeNearestSeeds(new Size(3, 3), null!));
        Assert.Throws<ArgumentNullException>(() => JumpFlooding.ComputeNearestSeeds(null!));
    }

    [Fact]
    public void NonSequentialMatrixView_ShouldMatchMaterializedBoundarySeedsAndDistanceTransform()
    {
        SequentialMatrix<bool> source = TestMatrices.CenteredBox(10, 8, 2, 2, 4, 4, true, false);
        source[4, 4] = false;
        source[7, 5] = true;

        var view = new MatrixView<bool>(source, 1, 1, 7, 5, EdgeHandling.Extend);
        MaskMatrix materialized = view.ToMaskMatrix();

        Assert.Equal(
            JumpFlooding.ExtractBoundarySeeds(materialized, backgroundConnectivity: GridConnectivity.EightConnected),
            JumpFlooding.ExtractBoundarySeeds(view, backgroundConnectivity: GridConnectivity.EightConnected));
        Assert.Equal(
            JumpFlooding.ExtractBoundarySeeds(materialized, backgroundConnectivity: GridConnectivity.FourConnected),
            JumpFlooding.ExtractBoundarySeeds(view, backgroundConnectivity: GridConnectivity.FourConnected));

        MatrixAssert.Equal(JumpFlooding.ComputeNearestSeeds(materialized), JumpFlooding.ComputeNearestSeeds(view));
        MatrixAssert.ApproxEqual(JumpFlooding.ComputeDistanceTransform(materialized), JumpFlooding.ComputeDistanceTransform(view), 1e-12);

        var emptyView = new MatrixView<bool>(source, 0, 0, 0, 3, EdgeHandling.Extend);
        Assert.Empty(JumpFlooding.ExtractBoundarySeeds(emptyView));
        Assert.Empty(JumpFlooding.ComputeNearestSeeds(emptyView));
        Assert.Empty(JumpFlooding.ComputeDistanceTransform(emptyView));
    }

    [Fact]
    public void AllEntrypoints_CancellationToken_ShouldHonorCancellationAndMatchUncanceledResults()
    {
        SequentialMatrix<bool> mask = TestMatrices.CenteredBox(16, 16, 5, 5, 10, 10, true, false);
        Coord[] seeds = [new(2, 2), new(12, 10)];

        using var canceledCts = new CancellationTokenSource();
        canceledCts.Cancel();
        CancellationToken canceled = canceledCts.Token;

        Assert.Throws<OperationCanceledException>(() => JumpFlooding.ExtractBoundarySeeds(mask, cancellationToken: canceled));
        Assert.Throws<OperationCanceledException>(() => JumpFlooding.ComputeNearestSeeds(mask.Size, seeds, canceled));
        Assert.Throws<OperationCanceledException>(() => JumpFlooding.ComputeNearestSeeds(mask, canceled));
        Assert.Throws<OperationCanceledException>(() => JumpFlooding.ComputeDistanceTransform(mask.Size, seeds, canceled));
        Assert.Throws<OperationCanceledException>(() => JumpFlooding.ComputeDistanceTransform(mask, canceled));
        Assert.Throws<OperationCanceledException>(() => JumpFlooding.ComputeNearestSeeds(new Size(0, 0), [], canceled));

        using var activeCts = new CancellationTokenSource();
        Matrix<NearestSeed> baselineSeeds = JumpFlooding.ComputeNearestSeeds(mask);
        Matrix<NearestSeed> tokenSeeds = JumpFlooding.ComputeNearestSeeds(mask, activeCts.Token);
        MatrixAssert.Equal(baselineSeeds, tokenSeeds);

        Matrix<DoublePixel> baselineDist = JumpFlooding.ComputeDistanceTransform(mask.Size, seeds);
        Matrix<DoublePixel> tokenDist = JumpFlooding.ComputeDistanceTransform(mask.Size, seeds, activeCts.Token);
        MatrixAssert.Equal(baselineDist, tokenDist);
    }
}

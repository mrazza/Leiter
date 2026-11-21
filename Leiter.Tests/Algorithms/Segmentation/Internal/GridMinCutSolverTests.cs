namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using System;
using System.Collections.Generic;
using System.Threading;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GridMinCutSolver"/>.
/// </summary>
public class GridMinCutSolverTests
{
    [Fact]
    public void GridMinCutSolver_KnownNetwork_ShouldComputeExactMaxFlowAndTriggerOrphanAdoption()
    {
        var solver = new GridMinCutSolver(3, 2);
        solver.SetUndirectedEdgeCapacity(0, 0, 5f);
        solver.SetUndirectedEdgeCapacity(1, 0, 2f);
        solver.SetUndirectedEdgeCapacity(0, 1, 2f);
        solver.SetUndirectedEdgeCapacity(1, 1, 4f);
        solver.SetUndirectedEdgeCapacity(2, 1, 6f);
        solver.SetUndirectedEdgeCapacity(3, 0, 3f);
        solver.SetUndirectedEdgeCapacity(4, 0, 1f);

        double[] sourceCaps = [100.0, 0.0, 0.0, 0.0, 0.0, 0.0];
        double[] sinkCaps = [0.0, 0.0, 100.0, 0.0, 0.0, 100.0];

        double flow = solver.Solve(sourceCaps, sinkCaps);
        Assert.Equal(3.0, flow, 4);
        Assert.True(solver.IsSourceSide(0));
        Assert.True(solver.IsSourceSide(1));
        Assert.True(solver.IsSourceSide(3));
        Assert.True(solver.IsSourceSide(4));
        Assert.False(solver.IsSourceSide(2));
        Assert.False(solver.IsSourceSide(5));

        double[] sourceCaps2 = [1.0, 1.0, 0.0, 2.0, 0.0, 0.0];
        double[] sinkCaps2 = [0.0, 0.0, 10.0, 0.0, 0.0, 10.0];
        double flow2 = solver.Solve(sourceCaps2, sinkCaps2);
        Assert.Equal(3.0, flow2, 4);

        Assert.Throws<ArgumentException>(() => solver.Solve([1.0], sinkCaps));
        Assert.Throws<ArgumentException>(() => solver.Solve(sourceCaps, [1.0]));
    }

    [Fact]
    public void GridMinCutSolver_ShouldExerciseSinkGrowthOrphanDemotionAndExcessPreFlow()
    {
        var solver = new GridMinCutSolver(4, 1);
        solver.SetUndirectedEdgeCapacity(0, 0, 10f);
        solver.SetUndirectedEdgeCapacity(1, 0, 10f);
        solver.SetUndirectedEdgeCapacity(2, 0, 2f);

        double[] src = [2.0, 1.0, 0.0, 0.0];
        double[] snk = [0.0, 1.0, 0.0, 10.0];
        double flow = solver.Solve(src, snk);
        Assert.Equal(3.0, flow, 4);
    }

    [Fact]
    public void GridMinCutSolver_CoordAndGridDirectionOverloads_ShouldMatchLinearIndices()
    {
        var solver = new GridMinCutSolver(new Size(3, 2));
        solver.SetUndirectedEdgeCapacity(new Coord(0, 0), GridDirection.East, 5f);
        solver.SetUndirectedEdgeCapacity(new Coord(1, 0), GridDirection.East, 2f);
        solver.SetUndirectedEdgeCapacity(new Coord(0, 0), GridDirection.South, 2f);
        solver.SetUndirectedEdgeCapacity(new Coord(1, 0), GridDirection.South, 4f);
        solver.SetUndirectedEdgeCapacity(new Coord(2, 0), GridDirection.South, 6f);
        solver.SetUndirectedEdgeCapacity(new Coord(0, 1), GridDirection.East, 3f);
        solver.SetUndirectedEdgeCapacity(new Coord(1, 1), GridDirection.East, 1f);

        double[] sourceCaps = [100.0, 0.0, 0.0, 0.0, 0.0, 0.0];
        double[] sinkCaps = [0.0, 0.0, 100.0, 0.0, 0.0, 100.0];

        double flow = solver.Solve(sourceCaps, sinkCaps);
        Assert.Equal(3.0, flow, 4);
        Assert.True(solver.IsSourceSide(new Coord(0, 0)));
        Assert.True(solver.IsSourceSide(new Coord(1, 0)));
        Assert.True(solver.IsSourceSide(new Coord(0, 1)));
        Assert.True(solver.IsSourceSide(new Coord(1, 1)));
        Assert.False(solver.IsSourceSide(new Coord(2, 0)));
        Assert.False(solver.IsSourceSide(new Coord(2, 1)));
    }

    [Fact]
    public void GridMinCutSolver_ChainWithMultipleOrphansAndReattachment_ShouldComputeExactMaxFlow()
    {
        // 5x2 grid with source at (0,0), sinks at (4,0) and (4,1), exercising multi-orphan reattachment in a single augmentation.
        var solver = new GridMinCutSolver(5, 2);
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 4; x++)
                solver.SetUndirectedEdgeCapacity(new Coord(x, y), GridDirection.East, 4f);
        }

        for (int x = 0; x < 5; x++)
            solver.SetUndirectedEdgeCapacity(new Coord(x, 0), GridDirection.South, x == 2 ? 1f : 4f);

        double[] sourceCaps = [10.0, 0.0, 0.0, 0.0, 0.0, 10.0, 0.0, 0.0, 0.0, 0.0];
        double[] sinkCaps = [0.0, 0.0, 0.0, 0.0, 10.0, 0.0, 0.0, 0.0, 0.0, 10.0];

        double flow = solver.Solve(sourceCaps, sinkCaps);
        Assert.Equal(8.0, flow, 4);
    }

    [Fact]
    public void Solve_CanceledToken_ShouldThrowOperationCanceledException()
    {
        var solver = new GridMinCutSolver(3, 2);
        double[] sourceCaps = [10.0, 0.0, 0.0, 0.0, 0.0, 0.0];
        double[] sinkCaps = [0.0, 0.0, 10.0, 0.0, 0.0, 10.0];

        using var canceledCts = new CancellationTokenSource();
        canceledCts.Cancel();

        Assert.Throws<OperationCanceledException>(() => solver.Solve(sourceCaps, sinkCaps, canceledCts.Token));
    }

    [Theory]
    [InlineData(128, 128, 1)]
    [InlineData(260, 260, 65_536)]
    public void Solve_MultiBandEquivalence_Random8ConnectedGrid_ShouldMatchSingleBandFlowAndOptimalCut(
        int width,
        int height,
        int minParallelPixels)
    {
        var rng = new Random(42_128 + width);
        int count = width * height;
        var solver = new GridMinCutSolver(width, height);
        var forwardEdges = new List<(int First, int Second, float Capacity)>();

        // Populate 8-connected undirected capacities directly via GridDirection
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var p = new Coord(x, y);
                int pIdx = (y * width) + x;
                foreach (GridNeighbor neighbor in GridNeighborhood.Forward8Connected)
                {
                    Coord q = p + neighbor.Offset;
                    if ((uint)q.X >= (uint)width || (uint)q.Y >= (uint)height)
                        continue;

                    float cap = (float)(0.2 + (rng.NextDouble() * 8.0));
                    solver.SetUndirectedEdgeCapacity(p, neighbor.Direction, cap);
                    forwardEdges.Add((pIdx, (q.Y * width) + q.X, cap));
                }
            }
        }

        var sourceCaps = new double[count];
        var sinkCaps = new double[count];
        for (int i = 0; i < count; i++)
        {
            int x = i % width;
            int y = i / width;
            if (x < width / 3)
                sourceCaps[i] = 1.0 + (rng.NextDouble() * 12.0);
            else if (x >= (width * 2) / 3)
                sinkCaps[i] = 1.0 + (rng.NextDouble() * 12.0);
            else if ((x + y) % 5 == 0)
            {
                sourceCaps[i] = rng.NextDouble() * 2.0;
                sinkCaps[i] = rng.NextDouble() * 2.0;
            }
        }

        double baselineFlow = solver.Solve(sourceCaps, sinkCaps, maxBands: 1, minParallelPixels: minParallelPixels);
        double baselineCut = ComputeCutCapacity(solver, forwardEdges, sourceCaps, sinkCaps);
        Assert.Equal(baselineFlow, baselineCut, precision: 2);

        foreach (int bands in new[] { 2, 4, 8 })
        {
            double bandFlow = solver.Solve(sourceCaps, sinkCaps, maxBands: bands, minParallelPixels: minParallelPixels);
            double bandCut = ComputeCutCapacity(solver, forwardEdges, sourceCaps, sinkCaps);

            Assert.Equal(baselineFlow, bandFlow, tolerance: 1e-2);
            Assert.Equal(bandFlow, bandCut, tolerance: 1e-2);
        }

        // Verify timestamp dirty-flag recovery after a canceled solve.
        using var canceledCts = new CancellationTokenSource();
        canceledCts.Cancel();
        Assert.Throws<OperationCanceledException>(() => solver.Solve(sourceCaps, sinkCaps, maxBands: 4, cancellationToken: canceledCts.Token, minParallelPixels: minParallelPixels));

        double recoveredFlow = solver.Solve(sourceCaps, sinkCaps, maxBands: 4, minParallelPixels: minParallelPixels);
        Assert.Equal(baselineFlow, recoveredFlow, tolerance: 1e-2);
    }

    [Fact]
    public void Solve_SeamCrossingCorridor_ShouldMatchSingleBandAcrossAllBandCounts()
    {
        const int width = 64;
        const int height = 128; // Seam at row 64 / 32 / 96 with maxBands = 2, 4
        var solver = new GridMinCutSolver(width, height);
        var forwardEdges = new List<(int First, int Second, float Capacity)>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var p = new Coord(x, y);
                int pIdx = (y * width) + x;
                foreach (GridNeighbor neighbor in GridNeighborhood.Forward8Connected)
                {
                    Coord q = p + neighbor.Offset;
                    if ((uint)q.X >= (uint)width || (uint)q.Y >= (uint)height)
                        continue;

                    // Create high-capacity vertical bridge crossing horizontal seams at x = 16 and x = 48.
                    float cap = (p.X == 16 || p.X == 48) && neighbor.Direction == GridDirection.South
                        ? 25f
                        : 1f;
                    solver.SetUndirectedEdgeCapacity(p, neighbor.Direction, cap);
                    forwardEdges.Add((pIdx, (q.Y * width) + q.X, cap));
                }
            }
        }

        var sourceCaps = new double[width * height];
        var sinkCaps = new double[width * height];
        sourceCaps[(0 * width) + 16] = 100.0;
        sourceCaps[(10 * width) + 48] = 100.0;
        sinkCaps[((height - 1) * width) + 16] = 100.0;
        sinkCaps[((height - 10) * width) + 48] = 100.0;

        double singleFlow = solver.Solve(sourceCaps, sinkCaps, maxBands: 1, minParallelPixels: 1);
        double singleCut = ComputeCutCapacity(solver, forwardEdges, sourceCaps, sinkCaps);
        Assert.Equal(singleFlow, singleCut, tolerance: 1e-2);

        foreach (int bands in new[] { 2, 4 })
        {
            double multiFlow = solver.Solve(sourceCaps, sinkCaps, maxBands: bands, minParallelPixels: 1);
            double multiCut = ComputeCutCapacity(solver, forwardEdges, sourceCaps, sinkCaps);
            Assert.Equal(singleFlow, multiFlow, tolerance: 1e-2);
            Assert.Equal(multiFlow, multiCut, tolerance: 1e-2);
        }
    }

    private static double ComputeCutCapacity(
        GridMinCutSolver solver,
        List<(int First, int Second, float Capacity)> forwardEdges,
        double[] sourceCaps,
        double[] sinkCaps)
    {
        double cut = 0.0;
        for (int i = 0; i < sourceCaps.Length; i++)
        {
            bool srcSide = solver.IsSourceSide(i);
            cut += srcSide ? Math.Max(0.0, sinkCaps[i]) : Math.Max(0.0, sourceCaps[i]);
        }

        for (int i = 0; i < forwardEdges.Count; i++)
        {
            (int u, int v, float cap) = forwardEdges[i];
            if (solver.IsSourceSide(u) != solver.IsSourceSide(v))
                cut += cap;
        }

        return cut;
    }
}

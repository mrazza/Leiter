namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using Leiter.Algorithms.Segmentation.Internal;
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
}

namespace Leiter.Tests.Algorithms.Segmentation;

using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GrabCut"/>, <see cref="GrabCutOptions"/>, and internal max-flow/GMM primitives.
/// </summary>
public class GrabCutTests
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
    public void GaussianMixtureModel_OrchardBoumanAndRegularization_ShouldBehaveCleanly()
    {
        var gmm = new GaussianMixtureModel(componentCount: 3);

        gmm.InitializeAndFit(ReadOnlySpan<float>.Empty, ReadOnlySpan<int>.Empty);
        Assert.True(gmm.EvaluateNegativeLogLikelihood(10f, 20f, 30f) > 100.0);
        Assert.Equal(0, gmm.AssignBestComponent(10f, 20f, 30f));

        float[] rgb =
        [
            10f, 12f, 15f,
            18f, 14f, 19f,
            12f, 22f, 11f,
            200f, 210f, 220f,
            215f, 195f, 205f,
            205f, 220f, 210f,
        ];
        gmm.InitializeAndFit(rgb, [0, 1, 2, 3, 4, 5]);

        int darkCluster = gmm.AssignBestComponent(14f, 16f, 14f);
        int brightCluster = gmm.AssignBestComponent(208f, 208f, 212f);
        Assert.NotEqual(darkCluster, brightCluster);

        double inlierNll = gmm.EvaluateNegativeLogLikelihood(14f, 16f, 14f);
        double outlierNll = gmm.EvaluateNegativeLogLikelihood(20f, 250f, 10f);
        Assert.True(double.IsFinite(inlierNll));
        Assert.True(outlierNll > inlierNll);

        float[] uniform = [50f, 60f, 70f, 50f, 60f, 70f];
        gmm.InitializeAndFit(uniform, [0, 1]);
        Assert.True(double.IsFinite(gmm.EvaluateNegativeLogLikelihood(50f, 60f, 70f)));

        gmm.FitFromAssignedComponents(uniform, [1, 1], expectedLabel: 0, [0, 0], totalMemberCount: 0);
        Assert.True(gmm.EvaluateNegativeLogLikelihood(50f, 60f, 70f) > 100.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => new GaussianMixtureModel(0));
    }

    [Fact]
    public void Segment_BoundingRectangleAndTrimap_ShouldSeparateForegroundFromBackground()
    {
        var image = new SequentialMatrix<Rgb8>(24, 24);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                bool insideObject = x >= 8 && x < 16 && y >= 8 && y < 16;
                image[x, y] = insideObject
                    ? new Rgb8((byte)(210 + (x % 4) * 5), 25, 25)
                    : new Rgb8(20, (byte)(140 + (y % 4) * 5), 215);
            }
        }

        MaskMatrix rectMask = GrabCut.Segment(image, new Coord(5, 5), new Size(14, 14));
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                bool expected = x >= 8 && x < 16 && y >= 8 && y < 16;
                Assert.Equal(expected, rectMask[x, y]);
            }
        }

        var trimap = new TrimapMatrix(image.Size);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (x < 3 || y < 3)
                    trimap[x, y] = TrimapLabel.Background;
                else if (x >= 10 && x < 14 && y >= 10 && y < 14)
                    trimap[x, y] = TrimapLabel.Foreground;
                else if (x >= 8 && x < 16 && y >= 8 && y < 16)
                    trimap[x, y] = TrimapLabel.ProbableForeground;
                else
                    trimap[x, y] = TrimapLabel.ProbableBackground;
            }
        }

        MaskMatrix trimapMask = GrabCut.Segment(image, trimap);
        Assert.True(trimapMask[11, 11]);
        Assert.False(trimapMask[0, 0]);
        Assert.False(trimapMask[20, 20]);
    }

    [Fact]
    public void Segment_EdgeCasesAndValidation_ShouldBehaveCorrectly()
    {
        var solid = new SequentialMatrix<Rgb8>(6, 6);
        solid.SetAll(new Rgb8(100, 120, 140));
        MaskMatrix solidResult = GrabCut.Segment(solid, new Coord(2, 2), new Size(2, 2), new GrabCutOptions(Iterations: 2, MixtureComponentCount: 2));
        Assert.Equal(solid.Size, solidResult.Size);

        var tiny = new SequentialMatrix<Rgb8>(1, 1);
        tiny[0, 0] = new Rgb8(10, 20, 30);
        var tinyBg = new TrimapMatrix(1, 1);
        tinyBg[0, 0] = TrimapLabel.Background;
        Assert.False(GrabCut.Segment(tiny, tinyBg)[0, 0]);

        var tinyFg = new TrimapMatrix(1, 1);
        tinyFg[0, 0] = TrimapLabel.Foreground;
        Assert.True(GrabCut.Segment(tiny, tinyFg)[0, 0]);

        var softTrimap = new TrimapMatrix(solid.Size);
        softTrimap.SetAll(TrimapLabel.ProbableBackground);
        softTrimap[2, 2] = TrimapLabel.ProbableForeground;
        MaskMatrix softResult = GrabCut.Segment(solid, softTrimap, new GrabCutOptions(Iterations: 1, MixtureComponentCount: 1));
        Assert.Equal(solid.Size, softResult.Size);

        var emptyImg = new SequentialMatrix<Rgb8>(0, 0);
        var emptyTrimap = new TrimapMatrix(0, 0);
        Assert.Empty(GrabCut.Segment(emptyImg, emptyTrimap));
        Assert.Empty(GrabCut.Segment(emptyImg, new Coord(0, 0), new Size(2, 2)));

        var badTrimap = new TrimapMatrix(6, 6);
        badTrimap[0, 0] = (TrimapLabel)99;
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, badTrimap));
        Assert.Throws<ArgumentException>(() => GrabCut.Segment(solid, new TrimapMatrix(2, 2)));
        Assert.Throws<ArgumentException>(() => GrabCut.Segment(solid, new Coord(20, 20), new Size(2, 2)));
        Assert.Throws<ArgumentException>(() => GrabCut.Segment(solid, new Coord(0, 0), new Size(6, 6)));
        Assert.Throws<ArgumentNullException>(() => GrabCut.Segment(null!, new Coord(0, 0), new Size(2, 2)));
        Assert.Throws<ArgumentNullException>(() => GrabCut.Segment(null!, badTrimap));
        Assert.Throws<ArgumentNullException>(() => GrabCut.Segment(solid, null!));

        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, new Coord(1, 1), new Size(0, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, new Coord(1, 1), new Size(2, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, new Coord(1, 1), new Size(2, 2), new GrabCutOptions(Iterations: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, new Coord(1, 1), new Size(2, 2), new GrabCutOptions(MixtureComponentCount: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, new Coord(1, 1), new Size(2, 2), new GrabCutOptions(SmoothnessWeight: -1.0)));
    }
}

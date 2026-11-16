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
        // 3x2 grid (width=3, height=2):
        // p0(0,0) -5- p1(1,0) -2- p2(2,0)
        //  |2          |4          |6
        // p3(0,1) -3- p4(1,1) -1- p5(2,1)
        var solver = new GridMinCutSolver(3, 2);
        solver.SetUndirectedEdgeCapacity(0, 0, 5f); // p0-p1
        solver.SetUndirectedEdgeCapacity(1, 0, 2f); // p1-p2
        solver.SetUndirectedEdgeCapacity(0, 1, 2f); // p0-p3
        solver.SetUndirectedEdgeCapacity(1, 1, 4f); // p1-p4
        solver.SetUndirectedEdgeCapacity(2, 1, 6f); // p2-p5
        solver.SetUndirectedEdgeCapacity(3, 0, 3f); // p3-p4
        solver.SetUndirectedEdgeCapacity(4, 0, 1f); // p4-p5

        // First solve forces bottleneck on p1-p2 (2) and p4-p5 (1) -> min-cut between {p0,p1,p3,p4} and {p2,p5} = 3.0!
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

        // Second solve where p0 has tight S-cap=1 and p1 has S-cap=1, p5 has T-cap=10 (triggers orphan adoption when p0/p1 saturate).
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
        // Construct a network where S->node saturates, causing orphan cascade that falls back to FreeTree.
        var solver = new GridMinCutSolver(4, 1);
        solver.SetUndirectedEdgeCapacity(0, 0, 10f);
        solver.SetUndirectedEdgeCapacity(1, 0, 10f);
        solver.SetUndirectedEdgeCapacity(2, 0, 2f);

        // Node 1 starts with both S=1 and T=1 (pre-canceled min(1,1)=1) -> FreeTree initially,
        // node 0 has S=2 (active), node 3 has T=10 (active). Path 0->1->2->3 saturates 0->S (cap 2) and edge 2->3 (cap 2).
        double[] src = [2.0, 1.0, 0.0, 0.0];
        double[] snk = [0.0, 1.0, 0.0, 10.0];
        double flow = solver.Solve(src, snk);
        Assert.Equal(3.0, flow, 4);
    }

    [Fact]
    public void GaussianMixtureModel_OrchardBoumanAndRegularization_ShouldBehaveCleanly()
    {
        var gmm = new GaussianMixtureModel(componentCount: 3);

        // Empty sample set clears weights and falls back to positive empty-model penalty.
        gmm.InitializeAndFit(ReadOnlySpan<float>.Empty, ReadOnlySpan<int>.Empty);
        Assert.True(gmm.EvaluateNegativeLogLikelihood(10f, 20f, 30f) > 100.0);
        Assert.Equal(0, gmm.AssignBestComponent(10f, 20f, 30f));

        // 2 distinct separated clusters + off-axis spread to exercise Jacobi off-diagonal rotations.
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

        // Single-color identical cluster stops splitting early without error.
        float[] uniform = [50f, 60f, 70f, 50f, 60f, 70f];
        gmm.InitializeAndFit(uniform, [0, 1]);
        Assert.True(double.IsFinite(gmm.EvaluateNegativeLogLikelihood(50f, 60f, 70f)));

        // Calling FitFromAssignedComponents with totalMemberCount == 0 handles empty cleanly.
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

        // Bounding rectangle intentionally includes a border ring of blue background around the red square.
        Matrix<LongPixel> rectMask = GrabCut.Segment(image, new Coord(5, 5), new Size(14, 14));
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                long expected = x >= 8 && x < 16 && y >= 8 && y < 16 ? 1L : 0L;
                Assert.Equal(expected, rectMask[x, y].Value);
            }
        }

        // Explicit 4-state trimap using Foreground, Background, ProbableForeground, ProbableBackground.
        var trimap = new SequentialMatrix<LongPixel>(image.Size);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (x < 3 || y < 3)
                    trimap[x, y] = (long)TrimapLabel.Background;
                else if (x >= 10 && x < 14 && y >= 10 && y < 14)
                    trimap[x, y] = (long)TrimapLabel.Foreground;
                else if (x >= 8 && x < 16 && y >= 8 && y < 16)
                    trimap[x, y] = (long)TrimapLabel.ProbableForeground;
                else
                    trimap[x, y] = (long)TrimapLabel.ProbableBackground;
            }
        }

        Matrix<LongPixel> trimapMask = GrabCut.Segment(image, trimap);
        Assert.Equal(1L, trimapMask[11, 11].Value);
        Assert.Equal(0L, trimapMask[0, 0].Value);
        Assert.Equal(0L, trimapMask[20, 20].Value);
    }

    [Fact]
    public void Segment_EdgeCasesAndValidation_ShouldBehaveCorrectly()
    {
        // Solid flat-color image -> beta == 0.
        var solid = new SequentialMatrix<Rgb8>(6, 6);
        solid.SetAll(new Rgb8(100, 120, 140));
        Matrix<LongPixel> solidResult = GrabCut.Segment(solid, new Coord(2, 2), new Size(2, 2), new GrabCutOptions(Iterations: 2, MixtureComponentCount: 2));
        Assert.Equal(solid.Size, solidResult.Size);

        // 1x1 image (zero neighbor pairs) with all-background trimap and all-foreground trimap.
        var tiny = new SequentialMatrix<Rgb8>(1, 1);
        tiny[0, 0] = new Rgb8(10, 20, 30);
        var tinyBg = new SequentialMatrix<LongPixel>(1, 1);
        tinyBg[0, 0] = (long)TrimapLabel.Background;
        Assert.Equal(0L, GrabCut.Segment(tiny, tinyBg)[0, 0].Value);

        var tinyFg = new SequentialMatrix<LongPixel>(1, 1);
        tinyFg[0, 0] = (long)TrimapLabel.Foreground;
        Assert.Equal(1L, GrabCut.Segment(tiny, tinyFg)[0, 0].Value);

        // Probable-only trimap (no hard Background pixels) initializes background GMM from ProbableBackground.
        var softTrimap = new SequentialMatrix<LongPixel>(solid.Size);
        softTrimap.SetAll((long)TrimapLabel.ProbableBackground);
        softTrimap[2, 2] = (long)TrimapLabel.ProbableForeground;
        Matrix<LongPixel> softResult = GrabCut.Segment(solid, softTrimap, new GrabCutOptions(Iterations: 1, MixtureComponentCount: 1));
        Assert.Equal(solid.Size, softResult.Size);

        // Empty 0x0 images.
        var emptyImg = new SequentialMatrix<Rgb8>(0, 0);
        var emptyTrimap = new SequentialMatrix<LongPixel>(0, 0);
        Assert.Empty(GrabCut.Segment(emptyImg, emptyTrimap));
        Assert.Empty(GrabCut.Segment(emptyImg, new Coord(0, 0), new Size(2, 2)));

        // Invalid trimaps and arguments.
        var badTrimap = new SequentialMatrix<LongPixel>(6, 6);
        badTrimap[0, 0] = 99L;
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, badTrimap));
        Assert.Throws<ArgumentException>(() => GrabCut.Segment(solid, new SequentialMatrix<LongPixel>(2, 2)));
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

namespace Leiter.Tests.Algorithms.Segmentation;

using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GrabCut"/>.
/// </summary>
public class GrabCutTests
{
    [Fact]
    public void Segment_BoundingRectangleAndTrimap_ShouldSeparateForegroundFromBackground()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            24,
            24,
            (x, y) => x >= 8 && x < 16 && y >= 8 && y < 16
                ? new Rgb8((byte)(210 + ((x % 4) * 5)), 25, 25)
                : new Rgb8(20, (byte)(140 + ((y % 4) * 5)), 215));

        MaskMatrix rectMask = GrabCut.Segment(image, new Coord(5, 5), new Size(14, 14));
        SequentialMatrix<bool> expectedMask = TestMatrices.CenteredBox(24, 24, 8, 8, 15, 15, inside: true, outside: false);
        MatrixAssert.Equal(expectedMask, rectMask);

        SequentialMatrix<TrimapLabel> trimap = TestMatrices.Create(
            image.Width,
            image.Height,
            (x, y) =>
            {
                if (x < 3 || y < 3)
                    return TrimapLabel.Background;
                if (x >= 10 && x < 14 && y >= 10 && y < 14)
                    return TrimapLabel.Foreground;
                if (x >= 8 && x < 16 && y >= 8 && y < 16)
                    return TrimapLabel.ProbableForeground;
                return TrimapLabel.ProbableBackground;
            });

        MaskMatrix trimapMask = GrabCut.Segment(image, trimap);
        Assert.True(trimapMask[11, 11]);
        Assert.False(trimapMask[0, 0]);
        Assert.False(trimapMask[20, 20]);
    }

    [Fact]
    public void Segment_EdgeCasesAndValidation_ShouldBehaveCorrectly()
    {
        SequentialMatrix<Rgb8> solid = TestMatrices.Constant(6, 6, new Rgb8(100, 120, 140));
        MaskMatrix solidResult = GrabCut.Segment(solid, new Coord(2, 2), new Size(2, 2), new GrabCutOptions(Iterations: 2, MixtureComponentCount: 2));
        Assert.Equal(solid.Size, solidResult.Size);

        SequentialMatrix<Rgb8> tiny = TestMatrices.Constant(1, 1, new Rgb8(10, 20, 30));
        var tinyBg = new TrimapMatrix(1, 1);
        tinyBg[0, 0] = TrimapLabel.Background;
        Assert.False(GrabCut.Segment(tiny, tinyBg)[0, 0]);

        var tinyFg = new TrimapMatrix(1, 1);
        tinyFg[0, 0] = TrimapLabel.Foreground;
        Assert.True(GrabCut.Segment(tiny, tinyFg)[0, 0]);

        SequentialMatrix<TrimapLabel> softTrimap = TestMatrices.Constant(solid.Width, solid.Height, TrimapLabel.ProbableBackground);
        softTrimap[2, 2] = TrimapLabel.ProbableForeground;
        MaskMatrix softResult = GrabCut.Segment(solid, softTrimap, new GrabCutOptions(Iterations: 1, MixtureComponentCount: 1));
        Assert.Equal(solid.Size, softResult.Size);

        // Zero smoothness exercises the zero-capacity short-circuit in GrabCutSession.
        MaskMatrix zeroSmoothnessResult = GrabCut.Segment(solid, new Coord(2, 2), new Size(2, 2), new GrabCutOptions(SmoothnessWeight: 0.0));
        Assert.Equal(solid.Size, zeroSmoothnessResult.Size);

        var emptyImg = new SequentialMatrix<Rgb8>(0, 0);
        var emptyTrimap = new TrimapMatrix(0, 0);
        Assert.Empty(GrabCut.Segment(emptyImg, emptyTrimap));
        Assert.Empty(GrabCut.Segment(emptyImg, new Coord(0, 0), new Size(2, 2)));

        var badTrimap = new TrimapMatrix(6, 6);
        badTrimap[0, 0] = (TrimapLabel)99;
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, badTrimap));
        Assert.Throws<ArgumentException>("trimap", () => GrabCut.Segment(solid, new TrimapMatrix(2, 2)));
        Assert.Throws<ArgumentException>(() => GrabCut.Segment(solid, new Coord(20, 20), new Size(2, 2)));
        Assert.Throws<ArgumentException>(() => GrabCut.Segment(solid, new Coord(0, 0), new Size(6, 6)));
        Assert.Throws<ArgumentNullException>("image", () => GrabCut.Segment(null!, new Coord(0, 0), new Size(2, 2)));
        Assert.Throws<ArgumentNullException>("image", () => GrabCut.Segment(null!, badTrimap));
        Assert.Throws<ArgumentNullException>("trimap", () => GrabCut.Segment(solid, null!));

        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, new Coord(1, 1), new Size(0, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, new Coord(1, 1), new Size(2, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, new Coord(1, 1), new Size(2, 2), new GrabCutOptions(Iterations: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, new Coord(1, 1), new Size(2, 2), new GrabCutOptions(MixtureComponentCount: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.Segment(solid, new Coord(1, 1), new Size(2, 2), new GrabCutOptions(SmoothnessWeight: -1.0)));
    }
}

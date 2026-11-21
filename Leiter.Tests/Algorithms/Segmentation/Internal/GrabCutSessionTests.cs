namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using System;
using System.Threading;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GrabCutSession"/>.
/// </summary>
public class GrabCutSessionTests
{
    [Fact]
    public void Run_MultiRunReuseAcrossTwoTrimaps_ShouldProduceConsistentMasks()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            16,
            16,
            (x, y) => x >= 4 && x < 12 && y >= 4 && y < 12
                ? new Rgb8(220, 30, 30)
                : new Rgb8(20, 150, 220));

        GrabCutSession session = GrabCut.CreateSession(
            image,
            new GrabCutOptions(Iterations: 2, MixtureComponentCount: 3, SmoothnessWeight: 50.0));

        SequentialMatrix<TrimapLabel> trimap1 = TestMatrices.Create(
            image.Width,
            image.Height,
            (x, y) =>
            {
                if (x < 2 || y < 2 || x >= 14 || y >= 14)
                    return TrimapLabel.Background;
                if (x >= 6 && x < 10 && y >= 6 && y < 10)
                    return TrimapLabel.Foreground;
                return TrimapLabel.ProbableForeground;
            });

        MaskMatrix mask1 = session.Run(trimap1, iterations: 2);
        Assert.True(mask1[8, 8]);
        Assert.False(mask1[0, 0]);

        SequentialMatrix<TrimapLabel> trimap2 = TestMatrices.Create(
            image.Width,
            image.Height,
            (x, y) => x < 4 || y < 4 || x >= 12 || y >= 12
                ? TrimapLabel.Background
                : TrimapLabel.ProbableForeground);

        MaskMatrix mask2 = session.Run(trimap2, iterations: 2);
        Assert.True(mask2[8, 8]);
        Assert.False(mask2[1, 1]);
    }

    [Fact]
    public void Run_AllBackgroundOrAllForeground_ShouldShortCircuit()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(8, 8, new Rgb8(128, 128, 128));
        var session = new GrabCutSession(image, new GrabCutOptions(Iterations: 2, MixtureComponentCount: 2, SmoothnessWeight: 20.0));

        SequentialMatrix<TrimapLabel> allBackground = TestMatrices.Constant(image.Width, image.Height, TrimapLabel.Background);
        MaskMatrix bgMask = session.Run(allBackground, iterations: 2);
        MatrixAssert.Equal(TestMatrices.Constant(image.Width, image.Height, false), bgMask);

        SequentialMatrix<TrimapLabel> allForeground = TestMatrices.Constant(image.Width, image.Height, TrimapLabel.Foreground);
        MaskMatrix fgMask = session.Run(allForeground, iterations: 2);
        MatrixAssert.Equal(TestMatrices.Constant(image.Width, image.Height, true), fgMask);
    }

    [Fact]
    public void Run_InvalidTrimapLabel_ShouldThrowArgumentOutOfRangeException()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(4, 4, new Rgb8(50, 50, 50));
        var session = new GrabCutSession(image, new GrabCutOptions(Iterations: 1, MixtureComponentCount: 2, SmoothnessWeight: 10.0));

        var invalidTrimap = new TrimapMatrix(image.Size);
        invalidTrimap[0, 0] = (TrimapLabel)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => session.Run(invalidTrimap, iterations: 1));
    }

    [Fact]
    public void Run_SingleArgOverload_ShouldMatchExplicitDefaultIterations()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            16,
            16,
            (x, y) => x >= 4 && x < 12 && y >= 4 && y < 12
                ? new Rgb8(220, 30, 30)
                : new Rgb8(20, 150, 220));

        SequentialMatrix<TrimapLabel> trimap = TestMatrices.Create(
            image.Width,
            image.Height,
            (x, y) =>
            {
                if (x < 2 || y < 2 || x >= 14 || y >= 14)
                    return TrimapLabel.Background;
                if (x >= 6 && x < 10 && y >= 6 && y < 10)
                    return TrimapLabel.Foreground;
                return TrimapLabel.ProbableForeground;
            });

        GrabCutSession factorySession = GrabCut.CreateSession(image, GrabCutOptions.Default);
        var explicitSession = new GrabCutSession(image, GrabCutOptions.Default);

        MaskMatrix factoryMask = factorySession.Run(trimap);
        MaskMatrix explicitMask = explicitSession.Run(trimap, GrabCutOptions.Default.Iterations);

        MatrixAssert.Equal(explicitMask, factoryMask);
        Assert.True(factoryMask[8, 8]);
        Assert.False(factoryMask[0, 0]);
    }

    [Fact]
    public void Constructor_And_CreateSession_InvalidArguments_ShouldThrowExpectedExceptions()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(4, 4, new Rgb8(50, 50, 50));

        ArgumentNullException nullImageEx = Assert.Throws<ArgumentNullException>(() => new GrabCutSession(null!, GrabCutOptions.Default));
        Assert.Equal("image", nullImageEx.ParamName);

        ArgumentNullException nullOptionsEx = Assert.Throws<ArgumentNullException>(() => new GrabCutSession(image, null!));
        Assert.Equal("options", nullOptionsEx.ParamName);

        ArgumentNullException createNullImageEx = Assert.Throws<ArgumentNullException>(() => GrabCut.CreateSession(null!, GrabCutOptions.Default));
        Assert.Equal("image", createNullImageEx.ParamName);

        ArgumentNullException createNullOptionsEx = Assert.Throws<ArgumentNullException>(() => GrabCut.CreateSession(image, null!));
        Assert.Equal("options", createNullOptionsEx.ParamName);

        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.CreateSession(image, new GrabCutOptions(Iterations: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.CreateSession(image, new GrabCutOptions(Iterations: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.CreateSession(image, new GrabCutOptions(MixtureComponentCount: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.CreateSession(image, new GrabCutOptions(MixtureComponentCount: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.CreateSession(image, new GrabCutOptions(SmoothnessWeight: -0.1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.CreateSession(image, new GrabCutOptions(SmoothnessWeight: double.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCut.CreateSession(image, new GrabCutOptions(SmoothnessWeight: double.PositiveInfinity)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void Constructor_And_CreateSession_EmptyImage_ShouldSucceedAndReturnEmptyMask(int width, int height)
    {
        var image = new SequentialMatrix<Rgb8>(width, height);
        var trimap = new SequentialMatrix<TrimapLabel>(width, height);

        var explicitSession = new GrabCutSession(image, GrabCutOptions.Default);
        GrabCutSession factorySession = GrabCut.CreateSession(image, GrabCutOptions.Default);

        MaskMatrix explicitMask = explicitSession.Run(trimap);
        MaskMatrix factoryMask = factorySession.Run(trimap);

        Assert.Equal(image.Size, explicitMask.Size);
        Assert.Equal(image.Size, factoryMask.Size);
        Assert.Throws<ArgumentNullException>("trimap", () => explicitSession.Run(null!));
        Assert.Throws<ArgumentException>("trimap", () => explicitSession.Run(new TrimapMatrix(1, 1)));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 5)]
    [InlineData(5, 1)]
    public void Constructor_And_CreateSession_DegenerateOnePixelWideImage_ShouldSucceed(int width, int height)
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            width,
            height,
            (x, y) => (x + y) == 0 ? new Rgb8(220, 20, 20) : new Rgb8(20, 40, 220));
        SequentialMatrix<TrimapLabel> trimap = TestMatrices.Create(
            width,
            height,
            (x, y) => (x + y) switch
            {
                0 => TrimapLabel.Foreground,
                1 => TrimapLabel.ProbableForeground,
                _ => TrimapLabel.Background,
            });

        var explicitSession = new GrabCutSession(image, GrabCutOptions.Default);
        GrabCutSession factorySession = GrabCut.CreateSession(image, GrabCutOptions.Default);

        MaskMatrix explicitMask = explicitSession.Run(trimap);
        MaskMatrix factoryMask = factorySession.Run(trimap);

        MatrixAssert.Equal(explicitMask, factoryMask);
        Assert.True(factoryMask[0, 0]);
        if (width * height > 1)
            Assert.False(factoryMask[width - 1, height - 1]);
    }

    [Fact]
    public void SessionAndRun_CanceledToken_ShouldThrowOperationCanceledException()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            12,
            12,
            (x, y) => x >= 3 && x < 9 && y >= 3 && y < 9 ? new Rgb8(220, 30, 30) : new Rgb8(20, 150, 220));
        TrimapMatrix trimap = TrimapMatrix.FromBoundingBox(image.Size, new Coord(2, 2), new Size(8, 8));

        using var canceledCts = new CancellationTokenSource();
        canceledCts.Cancel();

        Assert.Throws<OperationCanceledException>(() => new GrabCutSession(image, GrabCutOptions.Default, canceledCts.Token));
        Assert.Throws<OperationCanceledException>(() => GrabCut.CreateSession(image, GrabCutOptions.Default, canceledCts.Token));

        GrabCutSession session = GrabCut.CreateSession(image, GrabCutOptions.Default);
        Assert.Throws<OperationCanceledException>(() => session.Run(trimap, canceledCts.Token));
        Assert.Throws<OperationCanceledException>(() => session.Run(trimap, iterations: 2, cancellationToken: canceledCts.Token));
    }

    [Fact]
    public void Run_LargeImageAboveParallelThreshold_ShouldProduceDeterministicCutAndValidateTrimap()
    {
        const int size = 280; // 78,400 pixels (> ParallelRunner threshold)
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            size,
            size,
            (x, y) => x >= 70 && x < 210 && y >= 70 && y < 210
                ? new Rgb8(215, 35, 45)
                : new Rgb8(25, 130, 210));

        TrimapMatrix trimap = TrimapMatrix.FromBoundingBox(image.Size, new Coord(50, 50), new Size(180, 180));
        var options = new GrabCutOptions(Iterations: 2, MixtureComponentCount: 4, SmoothnessWeight: 50.0);

        GrabCutSession session = GrabCut.CreateSession(image, options);
        MaskMatrix first = session.Run(trimap);
        MaskMatrix second = session.Run(trimap);

        MatrixAssert.Equal(first, second);
        Assert.True(first[140, 140]);
        Assert.False(first[5, 5]);

        var badTrimap = new TrimapMatrix(image.Size);
        badTrimap[size - 1, size - 1] = (TrimapLabel)123;
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => session.Run(badTrimap));
        Assert.Equal("trimap", ex.ParamName);
    }
}

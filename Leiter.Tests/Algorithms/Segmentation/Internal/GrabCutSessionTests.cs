namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GrabCutSession"/>.
/// </summary>
public class GrabCutSessionTests
{
    [Fact]
    public void Run_MultiRunReuseAcrossTwoTrimaps_ShouldProduceConsistentMasks()
    {
        var image = new SequentialMatrix<Rgb8>(16, 16);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                bool inside = x >= 4 && x < 12 && y >= 4 && y < 12;
                image[x, y] = inside ? new Rgb8(220, 30, 30) : new Rgb8(20, 150, 220);
            }
        }

        var session = new GrabCutSession(image, mixtureComponentCount: 3, smoothnessWeight: 50.0);

        var trimap1 = new TrimapMatrix(image.Size);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (x < 2 || y < 2 || x >= 14 || y >= 14)
                    trimap1[x, y] = TrimapLabel.Background;
                else if (x >= 6 && x < 10 && y >= 6 && y < 10)
                    trimap1[x, y] = TrimapLabel.Foreground;
                else
                    trimap1[x, y] = TrimapLabel.ProbableForeground;
            }
        }

        MaskMatrix mask1 = session.Run(trimap1, iterations: 2);
        Assert.True(mask1[8, 8]);
        Assert.False(mask1[0, 0]);

        var trimap2 = new TrimapMatrix(image.Size);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (x < 4 || y < 4 || x >= 12 || y >= 12)
                    trimap2[x, y] = TrimapLabel.Background;
                else
                    trimap2[x, y] = TrimapLabel.ProbableForeground;
            }
        }

        MaskMatrix mask2 = session.Run(trimap2, iterations: 2);
        Assert.True(mask2[8, 8]);
        Assert.False(mask2[1, 1]);
    }

    [Fact]
    public void Run_AllBackgroundOrAllForeground_ShouldShortCircuit()
    {
        var image = new SequentialMatrix<Rgb8>(8, 8);
        image.SetAll(new Rgb8(128, 128, 128));
        var session = new GrabCutSession(image, mixtureComponentCount: 2, smoothnessWeight: 20.0);

        var allBackground = new TrimapMatrix(image.Size);
        allBackground.SetAll(TrimapLabel.Background);
        MaskMatrix bgMask = session.Run(allBackground, iterations: 2);
        for (int i = 0; i < bgMask.Count; i++)
            Assert.False(bgMask[i]);

        var allForeground = new TrimapMatrix(image.Size);
        allForeground.SetAll(TrimapLabel.Foreground);
        MaskMatrix fgMask = session.Run(allForeground, iterations: 2);
        for (int i = 0; i < fgMask.Count; i++)
            Assert.True(fgMask[i]);
    }

    [Fact]
    public void Run_InvalidTrimapLabel_ShouldThrowArgumentOutOfRangeException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        image.SetAll(new Rgb8(50, 50, 50));
        var session = new GrabCutSession(image, mixtureComponentCount: 2, smoothnessWeight: 10.0);

        var invalidTrimap = new TrimapMatrix(image.Size);
        invalidTrimap[0, 0] = (TrimapLabel)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => session.Run(invalidTrimap, iterations: 1));
    }
}

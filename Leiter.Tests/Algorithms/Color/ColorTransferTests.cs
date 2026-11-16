namespace Leiter.Tests.Algorithms.Color;

using Leiter.Algorithms.Color;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="LabColorStatistics"/>, <see cref="WatercolorPalettes"/>, and <see cref="ColorTransfer"/>.
/// </summary>
public class ColorTransferTests
{
    [Fact]
    public void ComputeStatistics_KnownMatrix_ShouldReturnExactMeanAndStdDev()
    {
        var matrix = new SequentialMatrix<Lab32>(2, 2);
        matrix[0, 0] = new Lab32(40f, -10f, 5f);
        matrix[1, 0] = new Lab32(60f, 10f, 5f);
        matrix[0, 1] = new Lab32(60f, -10f, 15f);
        matrix[1, 1] = new Lab32(80f, 10f, 15f);

        LabColorStatistics stats = ColorTransfer.ComputeStatistics(matrix);

        Assert.Equal(60.0, stats.MeanL, 6);
        Assert.Equal(0.0, stats.MeanA, 6);
        Assert.Equal(10.0, stats.MeanB, 6);
        Assert.Equal(Math.Sqrt(200.0), stats.StdDevL, 6);
        Assert.Equal(10.0, stats.StdDevA, 6);
        Assert.Equal(5.0, stats.StdDevB, 6);

        LabColorStatistics emptyStats = ColorTransfer.ComputeStatistics(new SequentialMatrix<Lab32>(0, 0));
        Assert.Equal(default, emptyStats);
    }

    [Fact]
    public void Transfer_Lab32_ShouldMatchTargetMeanAndStdDev()
    {
        var source = new SequentialMatrix<Lab32>(4, 4);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
                source[x, y] = new Lab32(30f + (x * 10f) + (y * 2f), -12f + (x * 6f), -8f + (y * 7f));
        }

        var target = new LabColorStatistics(72.5, -5.0, 14.0, 12.0, 6.5, 9.0);
        Matrix<Lab32> transferred = ColorTransfer.Transfer(source, target);
        LabColorStatistics resultStats = ColorTransfer.ComputeStatistics(transferred);

        Assert.Equal(target.MeanL, resultStats.MeanL, 3);
        Assert.Equal(target.MeanA, resultStats.MeanA, 3);
        Assert.Equal(target.MeanB, resultStats.MeanB, 3);
        Assert.Equal(target.StdDevL, resultStats.StdDevL, 3);
        Assert.Equal(target.StdDevA, resultStats.StdDevA, 3);
        Assert.Equal(target.StdDevB, resultStats.StdDevB, 3);

        Matrix<Lab32> unchanged = ColorTransfer.Transfer(source, target, strength: 0.0);
        for (int i = 0; i < source.Count; i++)
            Assert.Equal(source[i], unchanged[i]);

        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(source, target, strength: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(source, target, strength: 1.1));
    }

    [Fact]
    public void Transfer_ZeroStdDevChannel_ShouldShiftMeanWithoutNaN()
    {
        var gray = new SequentialMatrix<Lab32>(3, 2);
        for (int y = 0; y < gray.Height; y++)
            for (int x = 0; x < gray.Width; x++)
                gray[x, y] = new Lab32(40f + (x * 10f), 0f, 0f);

        var target = new LabColorStatistics(70.0, -8.5, 16.25, 15.0, 7.0, 12.0);
        Matrix<Lab32> transferred = ColorTransfer.Transfer(gray, target);

        for (int i = 0; i < transferred.Count; i++)
        {
            Assert.False(float.IsNaN(transferred[i].L));
            Assert.False(float.IsNaN(transferred[i].A));
            Assert.False(float.IsNaN(transferred[i].B));
            Assert.Equal(-8.5, transferred[i].A, 4);
            Assert.Equal(16.25, transferred[i].B, 4);
        }
    }

    [Fact]
    public void SelectClosestPalette_And_TransferToClosestPalette_ShouldSelectExpectedPreset()
    {
        var coastalImg = new SequentialMatrix<Rgb8>(4, 4);
        Rgb8[] coastalSamples =
        [
            new Rgb8(248, 247, 242),
            new Rgb8(154, 192, 220),
            new Rgb8(118, 162, 204),
            new Rgb8(76, 102, 128)
        ];
        for (int i = 0; i < coastalImg.Count; i++)
            coastalImg[i] = coastalSamples[i % coastalSamples.Length];

        var (coastalOut, coastalSelected) = ColorTransfer.TransferToClosestPalette(coastalImg);
        Assert.Equal(WatercolorPalettes.CoastalCerulean, coastalSelected);
        Assert.Equal(coastalImg.Size, coastalOut.Size);

        var botanicalImg = new SequentialMatrix<Rgb8>(4, 4);
        Rgb8[] botanicalSamples =
        [
            new Rgb8(246, 244, 232),
            new Rgb8(158, 184, 132),
            new Rgb8(115, 148, 92),
            new Rgb8(76, 98, 74)
        ];
        for (int i = 0; i < botanicalImg.Count; i++)
            botanicalImg[i] = botanicalSamples[i % botanicalSamples.Length];

        var (botanicalOut, botanicalSelected) = ColorTransfer.TransferToClosestPalette(botanicalImg);
        Assert.Equal(WatercolorPalettes.BotanicalFoliage, botanicalSelected);
        Assert.Equal(botanicalImg.Size, botanicalOut.Size);

        Matrix<Rgb8> referenceTransfer = ColorTransfer.Transfer(coastalImg, botanicalImg);
        LabColorStatistics refResultStats = ColorTransfer.ComputeStatistics(referenceTransfer);
        LabColorStatistics botanicalStats = ColorTransfer.ComputeStatistics(botanicalImg);
        Assert.True(Math.Abs(refResultStats.MeanA - botanicalStats.MeanA) < 5.0);
        Assert.True(Math.Abs(refResultStats.MeanB - botanicalStats.MeanB) < 5.0);

        Assert.Throws<ArgumentException>(() =>
            ColorTransfer.SelectClosestPalette(LabColorStatistics.UnitWeights, Array.Empty<WatercolorPalette>()));
    }
}

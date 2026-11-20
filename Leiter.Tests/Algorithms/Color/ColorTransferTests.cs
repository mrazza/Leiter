namespace Leiter.Tests.Algorithms.Color;

using System;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="ColorTransfer"/>.
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

        Assert.Equal(default, ColorTransfer.ComputeStatistics(new SequentialMatrix<Rgb8>(0, 0)));
    }

    [Fact]
    public void Transfer_Lab32_ShouldMatchTargetMeanAndStdDev()
    {
        var source = new SequentialMatrix<Lab32>(4, 4);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                source[x, y] = new Lab32(30f + (x * 10f) + (y * 2f), -12f + (x * 6f), -8f + (y * 7f));
            }
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
        {
            Assert.Equal(source[i], unchanged[i]);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(source, target, strength: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(source, target, strength: 1.1));
    }

    [Fact]
    public void Transfer_ZeroStdDevChannel_ShouldShiftMeanWithoutNaN()
    {
        var gray = new SequentialMatrix<Lab32>(3, 2);
        for (int y = 0; y < gray.Height; y++)
        {
            for (int x = 0; x < gray.Width; x++)
            {
                gray[x, y] = new Lab32(40f + (x * 10f), 0f, 0f);
            }
        }

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
    public void SelectClosestProfile_And_TransferToClosestProfile_ShouldSelectExpectedPreset()
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
        {
            coastalImg[i] = coastalSamples[i % coastalSamples.Length];
        }

        var (coastalOut, coastalSelected) = ColorTransfer.TransferToClosestProfile(coastalImg, WatercolorPalettes.Defaults);
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
        {
            botanicalImg[i] = botanicalSamples[i % botanicalSamples.Length];
        }

        var (botanicalOut, botanicalSelected) = ColorTransfer.TransferToClosestProfile(botanicalImg, WatercolorPalettes.Defaults);
        Assert.Equal(WatercolorPalettes.BotanicalFoliage, botanicalSelected);
        Assert.Equal(botanicalImg.Size, botanicalOut.Size);

        Matrix<Rgb8> referenceTransfer = ColorTransfer.Transfer(coastalImg, botanicalImg);
        LabColorStatistics refResultStats = ColorTransfer.ComputeStatistics(referenceTransfer);
        LabColorStatistics botanicalStats = ColorTransfer.ComputeStatistics(botanicalImg);
        Assert.True(Math.Abs(refResultStats.MeanA - botanicalStats.MeanA) < 5.0);
        Assert.True(Math.Abs(refResultStats.MeanB - botanicalStats.MeanB) < 5.0);

        Assert.Throws<ArgumentNullException>(() =>
            ColorTransfer.SelectClosestProfile(LabColorStatistics.UnitWeights, null!));
        Assert.Throws<ArgumentException>(() =>
            ColorTransfer.SelectClosestProfile(LabColorStatistics.UnitWeights, Array.Empty<ColorTransferProfile>()));
    }

    [Fact]
    public void Transfer_OverloadsAndEdgeCases_ShouldWork()
    {
        var statA = new LabColorStatistics(10, 0, 0, 5, 0, 0);

        Assert.Throws<ArgumentNullException>(() =>
            ColorTransfer.SelectClosestProfile(LabColorStatistics.UnitWeights, new ColorTransferProfile[] { null! }));
        Assert.Throws<ArgumentNullException>(() =>
            ColorTransfer.SelectClosestProfile(LabColorStatistics.UnitWeights, new ColorTransferProfile[] { WatercolorPalettes.BalancedWash, null! }));

        var customWeights = new LabColorStatistics(2.0, 1.0, 1.0, 0.5, 0.5, 0.5);
        ColorTransferProfile weightedMatch = ColorTransfer.SelectClosestProfile(statA, WatercolorPalettes.Defaults, featureWeights: customWeights);
        Assert.NotNull(weightedMatch);

        var d65Palette = new ColorTransferProfile("Custom D65", new(65, 2, 8, 14, 5, 6), Illuminant.D65);

        var emptyLab = new SequentialMatrix<Lab32>(0, 0);
        Assert.Empty(ColorTransfer.Transfer(emptyLab, statA));

        var emptyRgb = new SequentialMatrix<Rgb8>(0, 0);
        var sampleRgb = new SequentialMatrix<Rgb8>(2, 2);
        sampleRgb[0, 0] = new Rgb8(60, 90, 120);
        sampleRgb[1, 0] = new Rgb8(100, 130, 160);
        sampleRgb[0, 1] = new Rgb8(140, 170, 190);
        sampleRgb[1, 1] = new Rgb8(200, 210, 220);

        Assert.Throws<ArgumentNullException>(() => ColorTransfer.TransferToClosestProfile(sampleRgb, null!));
        Assert.Empty(ColorTransfer.Transfer(emptyRgb, sampleRgb));
        Matrix<Rgb8> refNoOp = ColorTransfer.Transfer(sampleRgb, sampleRgb, strength: 0.0);
        Assert.Equal(sampleRgb[0, 0], refNoOp[0, 0]);

        Assert.Empty(ColorTransfer.Transfer(emptyRgb, d65Palette));
        Matrix<Rgb8> palNoOp = ColorTransfer.Transfer(sampleRgb, d65Palette, strength: 0.0);
        Assert.Equal(sampleRgb[1, 1], palNoOp[1, 1]);

        Matrix<Rgb8> palTransferred = ColorTransfer.Transfer(sampleRgb, d65Palette);
        Assert.Equal(sampleRgb.Size, palTransferred.Size);

        var (emptyClosest, _) = ColorTransfer.TransferToClosestProfile(emptyRgb, [d65Palette]);
        Assert.Empty(emptyClosest);

        var (zeroStrengthClosest, _) = ColorTransfer.TransferToClosestProfile(sampleRgb, [d65Palette], strength: 0.0);
        Assert.Equal(sampleRgb[0, 1], zeroStrengthClosest[0, 1]);

        var (d65Out, d65Selected) = ColorTransfer.TransferToClosestProfile(sampleRgb, [d65Palette]);
        Assert.Equal(d65Palette, d65Selected);
        Assert.Equal(sampleRgb.Size, d65Out.Size);

        // Non-finite strength and IReadOnlyMatrix (MatrixView) inputs.
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(sampleRgb, d65Palette, strength: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(sampleRgb, d65Palette, strength: double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(sampleRgb, sampleRgb, strength: double.NegativeInfinity));

        var rgbView = new MatrixView<Rgb8>(sampleRgb, 0, 0, 2, 2, EdgeHandling.Extend);
        Matrix<Rgb8> viewTransferred = ColorTransfer.Transfer(rgbView, d65Palette);
        Assert.Equal(rgbView.Size, viewTransferred.Size);

        var viewClosestAndPalette = ColorTransfer.TransferToClosestProfile(rgbView, [d65Palette], strength: 0.0);
        Assert.Equal(d65Palette, viewClosestAndPalette.SelectedProfile);
        Assert.Equal(sampleRgb[1, 0], viewClosestAndPalette.Result[1, 0]);
    }

    [Fact]
    public void TransferToClosestProfile_GrayscaleImageWithDefaultWeights_ShouldSelectMistyMonochromeAndStayNearAchromatic()
    {
        var gray = new SequentialMatrix<Rgb8>(8, 8);
        for (int y = 0; y < gray.Height; y++)
        {
            for (int x = 0; x < gray.Width; x++)
            {
                byte v = (byte)(60 + (x * 20));
                gray[x, y] = new Rgb8(v, v, v);
            }
        }

        var (result, selected) = ColorTransfer.TransferToClosestProfile(
            gray,
            WatercolorPalettes.Defaults,
            featureWeights: WatercolorPalettes.DefaultFeatureWeights);

        Assert.Same(WatercolorPalettes.MistyMonochrome, selected);

        LabColorStatistics resultStats = ColorTransfer.ComputeStatistics(result);
        Assert.True(Math.Abs(resultStats.MeanA) < 2.0, $"Expected near-achromatic MeanA, got {resultStats.MeanA}");
        Assert.True(Math.Abs(resultStats.MeanB) < 4.0, $"Expected near-achromatic MeanB, got {resultStats.MeanB}");
    }

    [Fact]
    public void RgbMethods_WithInvalidEnumArguments_ShouldThrowEagerlyEvenWhenEmpty()
    {
        var emptyRgb = new SequentialMatrix<Rgb8>(0, 0);
        var profile = WatercolorPalettes.BalancedWash;

        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.ComputeStatistics(emptyRgb, (RgbColorSpace)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.ComputeStatistics(emptyRgb, RgbColorSpace.SRgb, (Illuminant)999));

        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(emptyRgb, emptyRgb, colorSpace: (RgbColorSpace)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(emptyRgb, emptyRgb, illuminant: (Illuminant)999));

        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(emptyRgb, profile, colorSpace: (RgbColorSpace)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.TransferToClosestProfile(emptyRgb, [profile], colorSpace: (RgbColorSpace)999));
    }

    [Fact]
    public void Transfer_And_TransferToClosestProfile_WithUndefinedProfileIlluminant_ShouldThrowWithExpectedParamName()
    {
        var emptyRgb = new SequentialMatrix<Rgb8>(0, 0);
        var sampleRgb = new SequentialMatrix<Rgb8>(2, 2);
        sampleRgb.SetAll(new Rgb8(100, 120, 140));

        var badIlluminantProfile = new ColorTransferProfile("Invalid", new LabColorStatistics(50, 0, 0, 10, 5, 5), (Illuminant)999);
        var validProfile = WatercolorPalettes.BalancedWash;

        var transferEx = Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(emptyRgb, badIlluminantProfile));
        Assert.Equal("profile", transferEx.ParamName);

        var closestEmptyEx = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ColorTransfer.TransferToClosestProfile(emptyRgb, [badIlluminantProfile]));
        Assert.Equal("profiles", closestEmptyEx.ParamName);

        var closestZeroStrengthEx = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ColorTransfer.TransferToClosestProfile(sampleRgb, [validProfile, badIlluminantProfile], strength: 0.0));
        Assert.Equal("profiles", closestZeroStrengthEx.ParamName);

        var closestNonEmptyEx = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ColorTransfer.TransferToClosestProfile(sampleRgb, [validProfile, badIlluminantProfile]));
        Assert.Equal("profiles", closestNonEmptyEx.ParamName);
    }

    [Fact]
    public void ColorTransferProfile_Validate_ShouldValidateNameIlluminantAndStatistics()
    {
        foreach (ColorTransferProfile preset in WatercolorPalettes.Defaults)
        {
            preset.Validate();
        }

        new ColorTransferProfile("Custom D65", new LabColorStatistics(50.0, -5.0, 12.0, 0.0, 0.0, 0.0), Illuminant.D65).Validate();

        var nullNameEx = Assert.Throws<ArgumentNullException>(() =>
            new ColorTransferProfile(null!, WatercolorPalettes.BalancedWash.Statistics).Validate());
        Assert.Equal(nameof(ColorTransferProfile.Name), nullNameEx.ParamName);

        foreach (string badName in new[] { string.Empty, "   " })
        {
            var nameEx = Assert.Throws<ArgumentException>(() =>
                new ColorTransferProfile(badName, WatercolorPalettes.BalancedWash.Statistics).Validate());
            Assert.Equal(nameof(ColorTransferProfile.Name), nameEx.ParamName);
        }

        var illuminantEx = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ColorTransferProfile("BadIlluminant", WatercolorPalettes.BalancedWash.Statistics, (Illuminant)999).Validate());
        Assert.Equal(nameof(ColorTransferProfile.Illuminant), illuminantEx.ParamName);

        LabColorStatistics[] invalidStats =
        [
            new(double.NaN, 0.0, 0.0, 1.0, 1.0, 1.0),
            new(50.0, double.PositiveInfinity, 0.0, 1.0, 1.0, 1.0),
            new(50.0, 0.0, double.NegativeInfinity, 1.0, 1.0, 1.0),
            new(50.0, 0.0, 0.0, -0.01, 1.0, 1.0),
            new(50.0, 0.0, 0.0, 1.0, double.NaN, 1.0),
            new(50.0, 0.0, 0.0, 1.0, 1.0, double.PositiveInfinity)
        ];

        foreach (LabColorStatistics stats in invalidStats)
        {
            var statsEx = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ColorTransferProfile("BadStats", stats).Validate());
            Assert.Equal(nameof(ColorTransferProfile.Statistics), statsEx.ParamName);
        }
    }

    [Fact]
    public void Transfer_And_TransferToClosestProfile_WithInvalidProfileNameOrStats_ShouldThrowWithExpectedParamName()
    {
        var sampleRgb = new SequentialMatrix<Rgb8>(2, 2);
        sampleRgb.SetAll(new Rgb8(100, 120, 140));
        ColorTransferProfile validProfile = WatercolorPalettes.BalancedWash;

        var emptyNameProfile = new ColorTransferProfile("   ", validProfile.Statistics);
        var badStatsProfile = new ColorTransferProfile("BadStats", new LabColorStatistics(50.0, 0.0, 0.0, -1.0, 5.0, 5.0));

        var transferNameEx = Assert.Throws<ArgumentException>(() => ColorTransfer.Transfer(sampleRgb, emptyNameProfile));
        Assert.Equal("profile", transferNameEx.ParamName);

        var transferStatsEx = Assert.Throws<ArgumentOutOfRangeException>(() => ColorTransfer.Transfer(sampleRgb, badStatsProfile));
        Assert.Equal("profile", transferStatsEx.ParamName);

        var closestNameEx = Assert.Throws<ArgumentException>(() =>
            ColorTransfer.TransferToClosestProfile(sampleRgb, [validProfile, emptyNameProfile]));
        Assert.Equal("profiles", closestNameEx.ParamName);

        var closestStatsEx = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ColorTransfer.TransferToClosestProfile(sampleRgb, [validProfile, badStatsProfile]));
        Assert.Equal("profiles", closestStatsEx.ParamName);
    }

    [Fact]
    public void ComputeStatistics_And_Transfer_WithLab32MatrixView_ShouldMatchSequentialMatrix()
    {
        var full = new SequentialMatrix<Lab32>(6, 5);
        for (int y = 0; y < full.Height; y++)
        {
            for (int x = 0; x < full.Width; x++)
            {
                full[x, y] = new Lab32(25f + (x * 9f) + (y * 3f), -15f + (x * 5f), -10f + (y * 6f));
            }
        }

        var view = new MatrixView<Lab32>(full, 1, 1, 4, 3, EdgeHandling.Extend);
        SequentialMatrix<Lab32> materialized = view.ToSequentialMatrix();

        LabColorStatistics viewStats = ColorTransfer.ComputeStatistics(view);
        LabColorStatistics seqStats = ColorTransfer.ComputeStatistics(materialized);

        Assert.Equal(seqStats.MeanL, viewStats.MeanL, 12);
        Assert.Equal(seqStats.MeanA, viewStats.MeanA, 12);
        Assert.Equal(seqStats.MeanB, viewStats.MeanB, 12);
        Assert.Equal(seqStats.StdDevL, viewStats.StdDevL, 12);
        Assert.Equal(seqStats.StdDevA, viewStats.StdDevA, 12);
        Assert.Equal(seqStats.StdDevB, viewStats.StdDevB, 12);

        var target = new LabColorStatistics(68.0, 4.0, -7.0, 10.0, 6.0, 8.0);
        Matrix<Lab32> viewTransferred = ColorTransfer.Transfer(view, target);
        Matrix<Lab32> seqTransferred = ColorTransfer.Transfer(materialized, target);
        Assert.True(viewTransferred.ContentEquals(seqTransferred));
    }
}

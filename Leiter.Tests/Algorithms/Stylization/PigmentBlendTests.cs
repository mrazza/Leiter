namespace Leiter.Tests.Algorithms.Stylization;

using System;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="PigmentBlend"/>.
/// </summary>
public class PigmentBlendTests
{
    private const double PrecisionTolerance = 1e-6;

    /// <summary>
    /// Verifies that paper white (1.0) and pure black (0.0) remain invariant under positive and negative density offsets.
    /// </summary>
    /// <param name="channel">The channel boundary value (0.0 or 1.0).</param>
    /// <param name="offset">The density adjustment offset.</param>
    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(0.0, -0.5)]
    [InlineData(1.0, 0.5)]
    [InlineData(1.0, -0.5)]
    public void AdjustChannelDensity_Endpoints_ShouldRemainInvariant(double channel, double offset)
    {
        double result = PigmentBlend.AdjustChannelDensity(channel, offset);
        Assert.Equal(channel, result, PrecisionTolerance);
    }

    /// <summary>
    /// Verifies that a positive density offset darkens midtones according to the quadratic formula.
    /// </summary>
    [Fact]
    public void AdjustChannelDensity_PositiveOffset_ShouldDarkenMidtones()
    {
        // c = 0.5, offset = 0.4: c * (1 - c) * offset = 0.25 * 0.4 = 0.1 => 0.5 - 0.1 = 0.4
        double result = PigmentBlend.AdjustChannelDensity(0.5, 0.4);
        Assert.Equal(0.4, result, PrecisionTolerance);
        Assert.True(result < 0.5);
    }

    /// <summary>
    /// Verifies that a negative density offset lightens midtones according to the quadratic formula.
    /// </summary>
    [Fact]
    public void AdjustChannelDensity_NegativeOffset_ShouldLightenMidtones()
    {
        // c = 0.5, offset = -0.4: c * (1 - c) * offset = 0.25 * (-0.4) = -0.1 => 0.5 - (-0.1) = 0.6
        double result = PigmentBlend.AdjustChannelDensity(0.5, -0.4);
        Assert.Equal(0.6, result, PrecisionTolerance);
        Assert.True(result > 0.5);
    }

    /// <summary>
    /// Verifies that out-of-range input channel values and extreme offsets are clamped safely to [0.0, 1.0].
    /// </summary>
    /// <param name="channel">The input channel value.</param>
    /// <param name="offset">The density offset.</param>
    /// <param name="expected">The expected clamped output.</param>
    [Theory]
    [InlineData(-0.5, 0.2, 0.0)]
    [InlineData(1.5, 0.2, 1.0)]
    [InlineData(0.5, 10.0, 0.0)]
    [InlineData(0.5, -10.0, 1.0)]
    public void AdjustChannelDensity_OutOfRangeValues_ShouldClampToUnitInterval(double channel, double offset, double expected)
    {
        double result = PigmentBlend.AdjustChannelDensity(channel, offset);
        Assert.Equal(expected, result, PrecisionTolerance);
    }

    /// <summary>
    /// Verifies that non-finite channel values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="channel">The non-finite channel value.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AdjustChannelDensity_NonFiniteChannel_ShouldThrowArgumentOutOfRangeException(double channel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.AdjustChannelDensity(channel, 0.0));
    }

    /// <summary>
    /// Verifies that non-finite density offsets throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="offset">The non-finite density offset.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AdjustChannelDensity_NonFiniteOffset_ShouldThrowArgumentOutOfRangeException(double offset)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.AdjustChannelDensity(0.5, offset));
    }

    /// <summary>
    /// Verifies that <see cref="PigmentBlend.AdjustDensity(Rgb64, double)"/> adjusts all channels uniformly.
    /// </summary>
    [Fact]
    public void AdjustDensity_Rgb64_ValidInputs_ShouldAdjustAllChannels()
    {
        var color = new Rgb64(0.5, 0.5, 0.5);
        Rgb64 result = PigmentBlend.AdjustDensity(color, 0.4);

        Assert.Equal(0.4, result.R, PrecisionTolerance);
        Assert.Equal(0.4, result.G, PrecisionTolerance);
        Assert.Equal(0.4, result.B, PrecisionTolerance);
    }

    /// <summary>
    /// Verifies that <see cref="PigmentBlend.AdjustDensity(Rgb64, double)"/> throws <see cref="ArgumentOutOfRangeException"/> when any channel is non-finite.
    /// </summary>
    [Fact]
    public void AdjustDensity_Rgb64_NonFiniteChannels_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.AdjustDensity(new Rgb64(double.NaN, 0.5, 0.5), 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.AdjustDensity(new Rgb64(0.5, double.PositiveInfinity, 0.5), 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.AdjustDensity(new Rgb64(0.5, 0.5, double.NegativeInfinity), 0.0));
    }

    /// <summary>
    /// Verifies that <see cref="PigmentBlend.AdjustDensity(Rgb64, double)"/> throws <see cref="ArgumentOutOfRangeException"/> when density offset is non-finite.
    /// </summary>
    /// <param name="offset">The non-finite density offset.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AdjustDensity_Rgb64_NonFiniteOffset_ShouldThrowArgumentOutOfRangeException(double offset)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.AdjustDensity(new Rgb64(0.5, 0.5, 0.5), offset));
    }

    /// <summary>
    /// Verifies that <see cref="PigmentBlend.AdjustDensity(Rgb8, double)"/> correctly darkens and lightens byte pixels.
    /// </summary>
    [Fact]
    public void AdjustDensity_Rgb8_ValidInputs_ShouldDarkenAndLighten()
    {
        var midGray = new Rgb8(128, 128, 128);
        Rgb8 darkened = PigmentBlend.AdjustDensity(midGray, 0.5);
        Rgb8 lightened = PigmentBlend.AdjustDensity(midGray, -0.5);

        Assert.True(darkened.R < 128);
        Assert.True(darkened.G < 128);
        Assert.True(darkened.B < 128);

        Assert.True(lightened.R > 128);
        Assert.True(lightened.G > 128);
        Assert.True(lightened.B > 128);
    }

    /// <summary>
    /// Verifies that <see cref="PigmentBlend.AdjustDensity(Rgb8, double)"/> throws <see cref="ArgumentOutOfRangeException"/> on non-finite offsets.
    /// </summary>
    /// <param name="offset">The non-finite density offset.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AdjustDensity_Rgb8_NonFiniteOffset_ShouldThrowArgumentOutOfRangeException(double offset)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.AdjustDensity(new Rgb8(128, 128, 128), offset));
    }

    /// <summary>
    /// Verifies that <see cref="PigmentBlend.MixPigments(Rgb64, Rgb64, double)"/> with overlapDarkening 0 returns the darker color invariant.
    /// </summary>
    [Fact]
    public void MixPigments_Rgb64_OverlapZero_ShouldReturnDarkerColorInvariant()
    {
        var darker = new Rgb64(0.2, 0.3, 0.4);
        var lighter = new Rgb64(0.7, 0.8, 0.9);

        Rgb64 result = PigmentBlend.MixPigments(darker, lighter, 0.0);

        Assert.Equal(darker.R, result.R, PrecisionTolerance);
        Assert.Equal(darker.G, result.G, PrecisionTolerance);
        Assert.Equal(darker.B, result.B, PrecisionTolerance);
    }

    /// <summary>
    /// Verifies that <see cref="PigmentBlend.MixPigments(Rgb64, Rgb64, double)"/> with overlapDarkening 1 equals multiplicative glaze.
    /// </summary>
    [Fact]
    public void MixPigments_Rgb64_OverlapOne_ShouldEqualMultiplicativeGlaze()
    {
        var darker = new Rgb64(0.2, 0.3, 0.4);
        var lighter = new Rgb64(0.7, 0.8, 0.9);

        Rgb64 result = PigmentBlend.MixPigments(darker, lighter, 1.0);

        Assert.Equal(darker.R * lighter.R, result.R, PrecisionTolerance);
        Assert.Equal(darker.G * lighter.G, result.G, PrecisionTolerance);
        Assert.Equal(darker.B * lighter.B, result.B, PrecisionTolerance);
    }

    /// <summary>
    /// Verifies commutativity of <see cref="PigmentBlend.MixPigments(Rgb64, Rgb64, double)"/> when color intensities differ.
    /// </summary>
    [Fact]
    public void MixPigments_Rgb64_DifferingIntensities_ShouldBeCommutative()
    {
        var colorA = new Rgb64(0.1, 0.2, 0.3);
        var colorB = new Rgb64(0.6, 0.7, 0.8);

        Rgb64 result1 = PigmentBlend.MixPigments(colorA, colorB, 0.6);
        Rgb64 result2 = PigmentBlend.MixPigments(colorB, colorA, 0.6);

        Assert.Equal(result1.R, result2.R, PrecisionTolerance);
        Assert.Equal(result1.G, result2.G, PrecisionTolerance);
        Assert.Equal(result1.B, result2.B, PrecisionTolerance);
    }

    /// <summary>
    /// Verifies commutativity and tie-break behavior of <see cref="PigmentBlend.MixPigments(Rgb64, Rgb64, double)"/> when intensities match.
    /// </summary>
    [Fact]
    public void MixPigments_Rgb64_MatchingIntensities_ShouldBeDeterministic()
    {
        // Exactly identical colors
        var colorA = new Rgb64(0.4, 0.5, 0.6);
        var colorB = new Rgb64(0.4, 0.5, 0.6);

        Rgb64 resIdentical1 = PigmentBlend.MixPigments(colorA, colorB, 0.5);
        Rgb64 resIdentical2 = PigmentBlend.MixPigments(colorB, colorA, 0.5);

        Assert.Equal(resIdentical1.R, resIdentical2.R, PrecisionTolerance);
        Assert.Equal(resIdentical1.G, resIdentical2.G, PrecisionTolerance);
        Assert.Equal(resIdentical1.B, resIdentical2.B, PrecisionTolerance);

        // Same mean intensity (0.2), different hue
        var red = new Rgb64(0.6, 0.0, 0.0);
        var green = new Rgb64(0.0, 0.6, 0.0);

        // With overlap = 1.0 (pure glaze), order is strictly symmetric since a * b == b * a
        Rgb64 glaze1 = PigmentBlend.MixPigments(red, green, 1.0);
        Rgb64 glaze2 = PigmentBlend.MixPigments(green, red, 1.0);

        Assert.Equal(glaze1.R, glaze2.R, PrecisionTolerance);
        Assert.Equal(glaze1.G, glaze2.G, PrecisionTolerance);
        Assert.Equal(glaze1.B, glaze2.B, PrecisionTolerance);

        // Deterministic tie-break picks first color as darker when <= holds
        Rgb64 tie1 = PigmentBlend.MixPigments(red, green, 0.0);
        Assert.Equal(0.6, tie1.R, PrecisionTolerance);
        Assert.Equal(0.0, tie1.G, PrecisionTolerance);

        Rgb64 tie2 = PigmentBlend.MixPigments(green, red, 0.0);
        Assert.Equal(0.0, tie2.R, PrecisionTolerance);
        Assert.Equal(0.6, tie2.G, PrecisionTolerance);
    }

    /// <summary>
    /// Verifies that channels outside [0, 1] are clamped safely during pigment mixing.
    /// </summary>
    [Fact]
    public void MixPigments_Rgb64_OutOfRangeChannels_ShouldClampSafely()
    {
        var out1 = new Rgb64(-0.2, 1.5, 0.5);
        var out2 = new Rgb64(0.2, -0.3, 1.2);

        Rgb64 result = PigmentBlend.MixPigments(out1, out2, 0.5);

        Assert.InRange(result.R, 0.0, 1.0);
        Assert.InRange(result.G, 0.0, 1.0);
        Assert.InRange(result.B, 0.0, 1.0);
    }

    /// <summary>
    /// Verifies that the default overlapDarkening parameter is 0.5.
    /// </summary>
    [Fact]
    public void MixPigments_Rgb64_DefaultOverlap_ShouldBeHalf()
    {
        var darker = new Rgb64(0.2, 0.3, 0.4);
        var lighter = new Rgb64(0.7, 0.8, 0.9);

        Rgb64 defaultResult = PigmentBlend.MixPigments(darker, lighter);
        Rgb64 explicitHalfResult = PigmentBlend.MixPigments(darker, lighter, 0.5);

        Assert.Equal(explicitHalfResult.R, defaultResult.R, PrecisionTolerance);
        Assert.Equal(explicitHalfResult.G, defaultResult.G, PrecisionTolerance);
        Assert.Equal(explicitHalfResult.B, defaultResult.B, PrecisionTolerance);
    }

    /// <summary>
    /// Verifies that non-finite channels in first or second color throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void MixPigments_Rgb64_NonFiniteColorChannels_ShouldThrowArgumentOutOfRangeException()
    {
        var valid = new Rgb64(0.5, 0.5, 0.5);

        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.MixPigments(new Rgb64(double.NaN, 0.5, 0.5), valid));
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.MixPigments(new Rgb64(0.5, double.PositiveInfinity, 0.5), valid));
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.MixPigments(new Rgb64(0.5, 0.5, double.NegativeInfinity), valid));

        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.MixPigments(valid, new Rgb64(double.NaN, 0.5, 0.5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.MixPigments(valid, new Rgb64(0.5, double.PositiveInfinity, 0.5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.MixPigments(valid, new Rgb64(0.5, 0.5, double.NegativeInfinity)));
    }

    /// <summary>
    /// Verifies that invalid overlapDarkening values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="overlap">The invalid overlap darkening value.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void MixPigments_Rgb64_InvalidOverlap_ShouldThrowArgumentOutOfRangeException(double overlap)
    {
        var color = new Rgb64(0.5, 0.5, 0.5);
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.MixPigments(color, color, overlap));
    }

    /// <summary>
    /// Verifies that <see cref="PigmentBlend.MixPigments(Rgb8, Rgb8, double)"/> blends 8-bit pixels correctly.
    /// </summary>
    [Fact]
    public void MixPigments_Rgb8_ValidInputs_ShouldBlendCorrectly()
    {
        var first = new Rgb8(50, 60, 70);
        var second = new Rgb8(200, 210, 220);

        Rgb8 defaultResult = PigmentBlend.MixPigments(first, second);
        Rgb8 fullGlazeResult = PigmentBlend.MixPigments(first, second, 1.0);
        Rgb8 zeroDarkeningResult = PigmentBlend.MixPigments(first, second, 0.0);

        Assert.Equal((byte)50, zeroDarkeningResult.R);
        Assert.Equal((byte)60, zeroDarkeningResult.G);
        Assert.Equal((byte)70, zeroDarkeningResult.B);

        // Glaze: (50/255 * 200/255) * 255 ≈ 39
        Assert.Equal((byte)39, fullGlazeResult.R);
        Assert.Equal((byte)49, fullGlazeResult.G);
        Assert.Equal((byte)60, fullGlazeResult.B);

        // Default 0.5 blend: (50 + 39.215...) / 2 = 44.607... -> rounds to 45
        Assert.Equal((byte)45, defaultResult.R);
        Assert.Equal((byte)55, defaultResult.G);
        Assert.Equal((byte)65, defaultResult.B);
    }

    /// <summary>
    /// Verifies that <see cref="PigmentBlend.MixPigments(Rgb8, Rgb8, double)"/> throws <see cref="ArgumentOutOfRangeException"/> on invalid overlap.
    /// </summary>
    /// <param name="overlap">The invalid overlap darkening value.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void MixPigments_Rgb8_InvalidOverlap_ShouldThrowArgumentOutOfRangeException(double overlap)
    {
        var color = new Rgb8(100, 100, 100);
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.MixPigments(color, color, overlap));
    }

    /// <summary>
    /// Verifies both <see cref="PigmentBlend.ApplyDensityField"/> overloads and argument validation.
    /// </summary>
    [Fact]
    public void ApplyDensityField_BothOverloads_ShouldMatchAdjustDensityAndValidate()
    {
        var image = new SequentialMatrix<Rgb8>(2, 2);
        image[0, 0] = new Rgb8(120, 140, 160);
        image[1, 0] = new Rgb8(80, 100, 120);
        image[0, 1] = new Rgb8(200, 210, 220);
        image[1, 1] = new Rgb8(40, 60, 80);

        var field = new SequentialMatrix<DoublePixel>(2, 2);
        field[0, 0] = 0.25;
        field[1, 0] = -0.5;
        field[0, 1] = 1.0;
        field[1, 1] = 0.0;

        Matrix<Rgb8> scaled = PigmentBlend.ApplyDensityField(image, field, intensity: 0.8);
        Matrix<Rgb8> mapped = PigmentBlend.ApplyDensityField(image, field, static v => 0.8 * v);
        for (int i = 0; i < image.Count; i++)
        {
            Assert.Equal(PigmentBlend.AdjustDensity(image[i], 0.8 * field[i].Value), scaled[i]);
            Assert.Equal(scaled[i], mapped[i]);
        }

        Matrix<Rgb8> zeroIntensity = PigmentBlend.ApplyDensityField(image, field, intensity: 0.0);
        Assert.Equal(image[0, 0], zeroIntensity[0, 0]);

        var empty = new SequentialMatrix<Rgb8>(0, 0);
        Assert.Empty(PigmentBlend.ApplyDensityField(empty, new SequentialMatrix<DoublePixel>(0, 0)));

        Assert.Throws<ArgumentNullException>(() => PigmentBlend.ApplyDensityField(null!, field));
        Assert.Throws<ArgumentNullException>(() => PigmentBlend.ApplyDensityField(image, null!));
        Assert.Throws<ArgumentNullException>(() => PigmentBlend.ApplyDensityField(image, field, null!));
        Assert.Throws<ArgumentException>(() => PigmentBlend.ApplyDensityField(image, new SequentialMatrix<DoublePixel>(3, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PigmentBlend.ApplyDensityField(image, field, double.NaN));
    }
}

namespace Leiter.Tests.Algorithms.Color;

using System;
using Leiter.Algorithms.Color;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="ChannelLut"/>.
/// </summary>
public class ChannelLutTests
{
    [Fact]
    public void Build_And_Apply_UniformAndPerChannel_ShouldMapCorrectly()
    {
        byte[] invertLut = ChannelLut.Build(static t => 1.0 - t);
        Assert.Equal(256, invertLut.Length);
        Assert.Equal(255, invertLut[0]);
        Assert.Equal(0, invertLut[255]);

        byte[] identityLut = ChannelLut.Build(static t => t);
        byte[] halfLut = ChannelLut.Build(static t => t * 0.5);

        var image = new SequentialMatrix<Rgb8>(2, 1);
        image[0, 0] = new Rgb8(0, 100, 200);
        image[1, 0] = new Rgb8(255, 50, 128);

        Matrix<Rgb8> inverted = ChannelLut.Apply(image, invertLut);
        Assert.Equal(new Rgb8(255, 155, 55), inverted[0, 0]);
        Assert.Equal(new Rgb8(0, 205, 127), inverted[1, 0]);

        Matrix<Rgb8> mixed = ChannelLut.Apply(image, invertLut, identityLut, halfLut);
        Assert.Equal(new Rgb8(255, 100, 100), mixed[0, 0]);
        Assert.Equal(new Rgb8(0, 50, 64), mixed[1, 0]);
    }

    [Fact]
    public void Validation_And_EmptyImage_ShouldBehaveCorrectly()
    {
        var empty = new SequentialMatrix<Rgb8>(0, 0);
        byte[] identityLut = ChannelLut.Build(static t => t);
        Assert.Empty(ChannelLut.Apply(empty, identityLut));
        Assert.Empty(ChannelLut.Apply(empty, identityLut, identityLut, identityLut));

        Assert.Throws<ArgumentNullException>(() => ChannelLut.Build(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChannelLut.Build(static _ => double.NaN));

        var valid = new SequentialMatrix<Rgb8>(2, 2);
        var badLut = new byte[128];

        Assert.Throws<ArgumentNullException>(() => ChannelLut.Apply(null!, identityLut));
        Assert.Throws<ArgumentNullException>(() => ChannelLut.Apply(null!, identityLut, identityLut, identityLut));
        Assert.Throws<ArgumentException>(() => ChannelLut.Apply(valid, badLut));
        Assert.Throws<ArgumentException>(() => ChannelLut.Apply(valid, badLut, identityLut, identityLut));
        Assert.Throws<ArgumentException>(() => ChannelLut.Apply(valid, identityLut, badLut, identityLut));
        Assert.Throws<ArgumentException>(() => ChannelLut.Apply(valid, identityLut, identityLut, badLut));
    }
}

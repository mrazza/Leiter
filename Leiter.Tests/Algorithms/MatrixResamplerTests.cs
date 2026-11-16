namespace Leiter.Tests.Algorithms;

using System;
using Leiter.Algorithms;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MatrixResampler"/>.
/// </summary>
public class MatrixResamplerTests
{
    [Fact]
    public void ResizeBilinear_ShouldInterpolateAndPreserveIdentity()
    {
        var source = new SequentialMatrix<DoublePixel>(2, 2);
        source[0, 0] = 0.0;
        source[1, 0] = 1.0;
        source[0, 1] = 0.0;
        source[1, 1] = 1.0;

        Matrix<DoublePixel> sameSize = source.ResizeBilinear(source.Size);
        Assert.Equal(source.Size, sameSize.Size);
        Assert.Equal(0.0, sameSize[0, 0].Value, 9);
        Assert.Equal(1.0, sameSize[1, 0].Value, 9);

        Matrix<DoublePixel> upsampled = source.ResizeBilinear(new Size(4, 4));
        Assert.Equal(new Size(4, 4), upsampled.Size);
        Assert.Equal(0.0, upsampled[0, 0].Value, 9);
        Assert.InRange(upsampled[1, 1].Value, 0.2, 0.3);
        Assert.InRange(upsampled[2, 2].Value, 0.7, 0.8);
        Assert.Equal(1.0, upsampled[3, 3].Value, 9);

        var rgb = new SequentialMatrix<Rgb8>(4, 4);
        rgb.SetAll(new Rgb8(100, 150, 200));
        Matrix<Rgb8> downsampled = rgb.ResizeBilinear(new Size(2, 2));
        Assert.Equal(new Size(2, 2), downsampled.Size);
        Assert.Equal(new Rgb8(100, 150, 200), downsampled[0, 0]);
        Assert.Equal(new Rgb8(100, 150, 200), downsampled[1, 1]);
    }

    [Fact]
    public void ResizeNearest_And_ResizeMaskNearest_ShouldResampleCorrectly()
    {
        var mask = new MaskMatrix(2, 2);
        mask[0, 0] = true;
        mask[1, 1] = true;

        MaskMatrix upsampledMask = mask.ResizeMaskNearest(new Size(4, 4));
        Assert.Equal(new Size(4, 4), upsampledMask.Size);
        Assert.True(upsampledMask[0, 0]);
        Assert.True(upsampledMask[1, 1]);
        Assert.False(upsampledMask[3, 0]);
        Assert.False(upsampledMask[0, 3]);
        Assert.True(upsampledMask[2, 2]);
        Assert.True(upsampledMask[3, 3]);

        Matrix<bool> genericUpsampled = ((IReadOnlyMatrix<bool>)mask).ResizeNearest(new Size(4, 4));
        Assert.True(upsampledMask.ContentEquals(genericUpsampled));

        var single = new SequentialMatrix<DoublePixel>(1, 1);
        single[0, 0] = 0.75;
        Matrix<DoublePixel> tiled = single.ResizeNearest(new Size(3, 2));
        Assert.Equal(new Size(3, 2), tiled.Size);
        for (int i = 0; i < tiled.Count; i++)
            Assert.Equal(0.75, tiled[i].Value, 9);
    }

    [Fact]
    public void ResampleMethods_EdgeCasesAndValidation_ShouldBehaveCorrectly()
    {
        var nonEmpty = new SequentialMatrix<DoublePixel>(2, 2);
        var emptyDouble = new SequentialMatrix<DoublePixel>(0, 0);
        var emptyMask = new MaskMatrix(0, 0);

        Assert.Empty(nonEmpty.ResizeBilinear(new Size(0, 2)));
        Assert.Empty(nonEmpty.ResizeBilinear(new Size(2, 0)));
        Assert.Empty(emptyDouble.ResizeBilinear(new Size(0, 0)));
        Assert.Empty(emptyMask.ResizeMaskNearest(new Size(0, 0)));
        Assert.Empty(emptyMask.ResizeNearest(new Size(0, 3)));

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).ResizeBilinear(new Size(2, 2)));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).ResizeNearest(new Size(2, 2)));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<bool>)null!).ResizeMaskNearest(new Size(2, 2)));

        Assert.Throws<ArgumentOutOfRangeException>(() => nonEmpty.ResizeBilinear(new Size(-1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => nonEmpty.ResizeBilinear(new Size(2, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => nonEmpty.ResizeNearest(new Size(-1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => emptyMask.ResizeMaskNearest(new Size(2, -1)));

        Assert.Throws<ArgumentException>(() => emptyDouble.ResizeBilinear(new Size(2, 2)));
        Assert.Throws<ArgumentException>(() => emptyDouble.ResizeNearest(new Size(2, 2)));
        Assert.Throws<ArgumentException>(() => emptyMask.ResizeMaskNearest(new Size(2, 2)));
    }
}

namespace Leiter.Tests.Algorithms.AntiAliasing;

using System;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Fxaa"/>.
/// </summary>
public class FxaaTests
{
    /// <summary>
    /// Verifies that flat or low-contrast regions below the contrast thresholds remain bit-exact.
    /// </summary>
    [Fact]
    public void Apply_FlatAndLowContrastRegions_ShouldStayBitExact()
    {
        int size = 12;
        var solidImage = new SequentialMatrix<Rgb8>(size, size);
        var lowContrastImage = new SequentialMatrix<Rgb8>(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                solidImage[x, y] = new Rgb8(128, 64, 32);
                // Difference of 2 is well below default absolute threshold 0.0625 * 255 = ~16
                byte delta = (byte)((x + y) % 2 == 0 ? 0 : 2);
                lowContrastImage[x, y] = new Rgb8((byte)(100 + delta), 100, 100);
            }
        }

        Matrix<Rgb8> solidResult = Fxaa.Apply(solidImage);
        for (int i = 0; i < solidImage.Count; i++)
        {
            Assert.Equal(solidImage[i], solidResult[i]);
        }

        Matrix<Rgb8> lowContrastResult = Fxaa.Apply(lowContrastImage);
        for (int i = 0; i < lowContrastImage.Count; i++)
        {
            Assert.Equal(lowContrastImage[i], lowContrastResult[i]);
        }
    }

    /// <summary>
    /// Verifies that a high-contrast diagonal step edge gets anti-aliased along the boundary
    /// while flat interiors remain bit-exact.
    /// </summary>
    [Fact]
    public void Apply_DiagonalStepEdge_ShouldAntiAliasBoundaryAndPreserveFlatInteriors()
    {
        int size = 16;
        var image = new SequentialMatrix<Rgb8>(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                image[x, y] = (x + y < size) ? new Rgb8(0, 0, 0) : new Rgb8(255, 255, 255);
            }
        }

        Matrix<Rgb8> result = Fxaa.Apply(image);

        // Flat interior pixels away from the boundary should be completely preserved
        Assert.Equal(new Rgb8(0, 0, 0), result[0, 0]);
        Assert.Equal(new Rgb8(0, 0, 0), result[1, 1]);
        Assert.Equal(new Rgb8(255, 255, 255), result[size - 1, size - 1]);
        Assert.Equal(new Rgb8(255, 255, 255), result[size - 2, size - 2]);

        // Boundary pixels along the diagonal should be anti-aliased (neither pure black nor pure white)
        bool foundAntiAliased = false;
        for (int y = 1; y < size - 1; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                if (x + y == size - 1 || x + y == size)
                {
                    byte r = result[x, y].R;
                    if (r is > 0 and < 255)
                    {
                        foundAntiAliased = true;
                    }
                }
            }
        }

        Assert.True(foundAntiAliased, "Diagonal boundary pixels should be blended by FXAA.");
    }

    /// <summary>
    /// Verifies that both horizontal and vertical edge orientations are properly detected and anti-aliased.
    /// </summary>
    [Fact]
    public void Apply_HorizontalAndVerticalEdges_ShouldAntiAliasBothOrientations()
    {
        int size = 16;
        var horizontalImage = new SequentialMatrix<Rgb8>(size, size);
        var verticalImage = new SequentialMatrix<Rgb8>(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                horizontalImage[x, y] = y < 8 ? new Rgb8(0, 0, 0) : new Rgb8(255, 255, 255);
                verticalImage[x, y] = x < 8 ? new Rgb8(0, 0, 0) : new Rgb8(255, 255, 255);
            }
        }

        Matrix<Rgb8> horizontalResult = Fxaa.Apply(horizontalImage);
        Matrix<Rgb8> verticalResult = Fxaa.Apply(verticalImage);

        // Edge pixels on horizontal edge
        bool horizontalBlended = false;
        for (int x = 2; x < size - 2; x++)
        {
            if (horizontalResult[x, 7].R > 0 || horizontalResult[x, 8].R < 255)
            {
                horizontalBlended = true;
                break;
            }
        }

        // Edge pixels on vertical edge
        bool verticalBlended = false;
        for (int y = 2; y < size - 2; y++)
        {
            if (verticalResult[7, y].R > 0 || verticalResult[8, y].R < 255)
            {
                verticalBlended = true;
                break;
            }
        }

        Assert.True(horizontalBlended, "Horizontal edge should be anti-aliased.");
        Assert.True(verticalBlended, "Vertical edge should be anti-aliased.");
    }

    /// <summary>
    /// Verifies that SubpixelBlendStrength = 0 produces different blending behavior from the default strength.
    /// </summary>
    [Fact]
    public void Apply_SubpixelBlendStrength_ShouldAffectAntiAliasing()
    {
        int size = 16;
        var image = new SequentialMatrix<Rgb8>(size, size);

        // Isolated high-contrast point in flat background to maximize subpixel blend
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                image[x, y] = new Rgb8(0, 0, 0);
            }
        }

        image[8, 8] = new Rgb8(255, 255, 255);

        var optionsZero = new FxaaOptions(SubpixelBlendStrength: 0.0);
        Matrix<Rgb8> resultZero = Fxaa.Apply(image, optionsZero);
        Matrix<Rgb8> resultDefault = Fxaa.Apply(image);

        // At (8, 8), default subpixel blend should soften the isolated pixel more than zero blend
        Assert.NotEqual(resultZero[8, 8], resultDefault[8, 8]);
    }

    /// <summary>
    /// Verifies that empty and single-pixel matrices are handled cleanly without exceptions.
    /// </summary>
    [Fact]
    public void Apply_EmptyAndSmallMatrices_ShouldHandleGracefully()
    {
        var emptyImage = new SequentialMatrix<Rgb8>(0, 0);
        Matrix<Rgb8> emptyResult = Fxaa.Apply(emptyImage);
        Assert.Empty(emptyResult);

        var singlePixel = new SequentialMatrix<Rgb8>(1, 1);
        singlePixel[0, 0] = new Rgb8(200, 150, 100);
        Matrix<Rgb8> singleResult = Fxaa.Apply(singlePixel);
        Assert.Single(singleResult);
        Assert.Equal(new Rgb8(200, 150, 100), singleResult[0, 0]);

        var narrowRow = new SequentialMatrix<Rgb8>(1, 5);
        Matrix<Rgb8> narrowRowResult = Fxaa.Apply(narrowRow);
        Assert.Equal(5, narrowRowResult.Count);

        var narrowCol = new SequentialMatrix<Rgb8>(5, 1);
        Matrix<Rgb8> narrowColResult = Fxaa.Apply(narrowCol);
        Assert.Equal(5, narrowColResult.Count);
    }

    /// <summary>
    /// Verifies that null image and invalid options throw expected exceptions.
    /// </summary>
    [Fact]
    public void Apply_InvalidInputs_ShouldThrowAppropriateExceptions()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);

        Assert.Throws<ArgumentNullException>(() => Fxaa.Apply(null!));

        var invalidOptions = new FxaaOptions(RelativeContrastThreshold: 0.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => Fxaa.Apply(image, invalidOptions));
    }
}

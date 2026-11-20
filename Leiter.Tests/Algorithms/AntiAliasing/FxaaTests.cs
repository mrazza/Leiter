namespace Leiter.Tests.Algorithms.AntiAliasing;

using System;
using System.Threading;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
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
        SequentialMatrix<Rgb8> solidImage = TestMatrices.Constant(size, size, new Rgb8(128, 64, 32));
        // Difference of 2 is well below default absolute threshold 0.0625 * 255 = ~16
        SequentialMatrix<Rgb8> lowContrastImage = TestMatrices.Checkerboard(
            size,
            size,
            new Rgb8(100, 100, 100),
            new Rgb8(102, 100, 100));

        Matrix<Rgb8> solidResult = Fxaa.Apply(solidImage);
        MatrixAssert.Equal(solidImage, solidResult);

        Matrix<Rgb8> lowContrastResult = Fxaa.Apply(lowContrastImage);
        MatrixAssert.Equal(lowContrastImage, lowContrastResult);
    }

    /// <summary>
    /// Verifies that a high-contrast diagonal step edge gets anti-aliased along the boundary
    /// while flat interiors remain bit-exact.
    /// </summary>
    [Fact]
    public void Apply_DiagonalStepEdge_ShouldAntiAliasBoundaryAndPreserveFlatInteriors()
    {
        int size = 16;
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            size,
            size,
            (x, y) => (x + y < size) ? new Rgb8(0, 0, 0) : new Rgb8(255, 255, 255));

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
        var horizontalImage = TestMatrices.HorizontalSplit(size, size, new Rgb8(0, 0, 0), new Rgb8(255, 255, 255), splitY: 8);
        var verticalImage = TestMatrices.VerticalSplit(size, size, new Rgb8(0, 0, 0), new Rgb8(255, 255, 255), splitX: 8);

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
        // Isolated high-contrast point in flat background to maximize subpixel blend
        int size = 16;
        var image = TestMatrices.Constant(size, size, new Rgb8(0, 0, 0));

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

    /// <summary>
    /// Verifies that a 1-pixel thin vertical high-contrast line is classified vertically via second-derivative Laplacian test
    /// and softened horizontally rather than falling back to horizontal orientation with zero perpendicular gradient.
    /// </summary>
    [Fact]
    public void Apply_ThinVerticalLine_ShouldDetectVerticalOrientationAndBlendHorizontally()
    {
        const int size = 16;
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            size,
            size,
            (x, _) => x == 8 ? new Rgb8(255, 255, 255) : new Rgb8(0, 0, 0));

        Matrix<Rgb8> result = Fxaa.Apply(image);

        // Along the thin vertical line (x=8), FXAA must soften horizontally toward neighbors (< 255).
        Assert.True(result[8, 8].R < 255, $"Expected thin vertical line pixel (8,8) to blend below 255, got {result[8, 8].R}");
    }

    /// <summary>
    /// Verifies that zero contrast thresholds on uniform images (all-black with zero absolute threshold,
    /// and uniform gray where maxLuma * RelativeContrastThreshold underflows to zero) stay unchanged without NaN or exceptions.
    /// </summary>
    [Fact]
    public void Apply_UniformImagesWithZeroContrastThreshold_ShouldStayUnchangedWithoutExceptions()
    {
        var blackImage = TestMatrices.Constant(8, 8, new Rgb8(0, 0, 0));
        var zeroAbsOptions = new FxaaOptions(AbsoluteContrastThreshold: 0.0);
        Matrix<Rgb8> blackResult = Fxaa.Apply(blackImage, zeroAbsOptions);
        MatrixAssert.Equal(blackImage, blackResult);

        var uniformGrayImage = TestMatrices.Constant(8, 8, new Rgb8(64, 64, 64));
        var underflowOptions = new FxaaOptions(RelativeContrastThreshold: double.Epsilon, AbsoluteContrastThreshold: 0.0);
        Matrix<Rgb8> grayResult = Fxaa.Apply(uniformGrayImage, underflowOptions);
        MatrixAssert.Equal(uniformGrayImage, grayResult);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Fxaa.Apply(uniformGrayImage, new FxaaOptions(RelativeContrastThreshold: 0.0, AbsoluteContrastThreshold: 0.0)));
    }

    /// <summary>
    /// Verifies that a pre-canceled <see cref="CancellationToken"/> throws <see cref="OperationCanceledException"/>
    /// and that an active uncanceled token produces bit-for-bit identical output.
    /// </summary>
    [Fact]
    public void Apply_CanceledToken_ShouldThrowOperationCanceledException()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            16,
            16,
            (x, y) => (x + y < 16) ? new Rgb8(0, 0, 0) : new Rgb8(255, 255, 255));
        var empty = new SequentialMatrix<Rgb8>(0, 0);
        var canceledToken = new CancellationToken(canceled: true);

        Assert.Throws<OperationCanceledException>(() => Fxaa.Apply(image, cancellationToken: canceledToken));
        Assert.Throws<OperationCanceledException>(() => Fxaa.Apply(empty, cancellationToken: canceledToken));

        using var cts = new CancellationTokenSource();
        Matrix<Rgb8> expected = Fxaa.Apply(image);
        Matrix<Rgb8> actual = Fxaa.Apply(image, cancellationToken: cts.Token);
        MatrixAssert.Equal(expected, actual);
    }
}

namespace Leiter.Tests.Algorithms;

using System;
using Leiter.Algorithms;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="OrientedEllipticalFilter"/>.
/// </summary>
public class OrientedEllipticalFilterTests
{
    /// <summary>
    /// Verifies directional anisotropy: smoothing along the line orientation preserves perpendicular sharpness,
    /// whereas smoothing perpendicular to the line diffuses across rows.
    /// </summary>
    [Fact]
    public void Apply_DirectionalAnisotropy_ShouldSmoothAlongOrientationAndPreservePerpendicular()
    {
        int width = 21;
        int height = 21;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var horizontalOrientations = new SequentialMatrix<Vector2D>(width, height);
        var verticalOrientations = new SequentialMatrix<Vector2D>(width, height);

        // Draw a single horizontal white line at row 10 on a black background
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = y == 10 ? new Rgb8(255, 255, 255) : new Rgb8(0, 0, 0);
                horizontalOrientations[x, y] = new Vector2D(1.0, 0.0);
                verticalOrientations[x, y] = new Vector2D(0.0, 1.0);
            }
        }

        var options = new OrientedEllipticalFilterOptions(MajorRadius: 5.0, MinorRadius: 1.0, SigmaScale: 0.5);

        Matrix<Rgb8> horizontalFiltered = OrientedEllipticalFilter.Apply(image, horizontalOrientations, options);
        Matrix<Rgb8> verticalFiltered = OrientedEllipticalFilter.Apply(image, verticalOrientations, options);

        // At row 8 (2 pixels perpendicular to the line), horizontal smoothing (minor radius = 1.0)
        // should have virtually zero diffusion, while vertical smoothing (major radius = 5.0) diffuses strongly.
        byte horizontalPerpendicularLeak = horizontalFiltered[10, 8].R;
        byte verticalPerpendicularLeak = verticalFiltered[10, 8].R;

        Assert.True(horizontalPerpendicularLeak < verticalPerpendicularLeak);
        Assert.Equal(0, horizontalPerpendicularLeak);
        Assert.True(verticalPerpendicularLeak > 20);
    }

    /// <summary>
    /// Verifies that pixels masked out by <c>activeMask</c> remain unmodified.
    /// </summary>
    [Fact]
    public void Apply_ActiveMaskGating_ShouldPreserveUnmaskedPixels()
    {
        int size = 11;
        var image = new SequentialMatrix<Rgb8>(size, size);
        var orientations = new SequentialMatrix<Vector2D>(size, size);
        var mask = new MaskMatrix(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                image[x, y] = new Rgb8((byte)(x * 20), (byte)(y * 20), 100);
                orientations[x, y] = new Vector2D(1.0, 0.0);
                // Mask active only for the top half
                mask[x, y] = y < 5;
            }
        }

        Matrix<Rgb8> result = OrientedEllipticalFilter.Apply(image, orientations, null, mask);

        // Bottom half (mask = false) must remain bit-exact
        for (int y = 5; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Assert.Equal(image[x, y], result[x, y]);
            }
        }
    }

    /// <summary>
    /// Verifies that zero or non-finite orientation vectors safely fall back to the original pixel without modifying it.
    /// </summary>
    [Fact]
    public void Apply_ZeroOrNonFiniteOrientation_ShouldPreserveOriginalPixel()
    {
        int size = 5;
        var image = new SequentialMatrix<Rgb8>(size, size);
        var orientations = new SequentialMatrix<Vector2D>(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                image[x, y] = new Rgb8(120, 130, 140);
                orientations[x, y] = Vector2D.Zero;
            }
        }

        // Test with zero orientations
        Matrix<Rgb8> zeroResult = OrientedEllipticalFilter.Apply(image, orientations);
        for (int i = 0; i < image.Count; i++)
        {
            Assert.Equal(image[i], zeroResult[i]);
        }

        // Test with NaN orientations
        orientations[2, 2] = new Vector2D(double.NaN, 1.0);
        orientations[2, 3] = new Vector2D(1.0, double.PositiveInfinity);
        Matrix<Rgb8> nanResult = OrientedEllipticalFilter.Apply(image, orientations);
        Assert.Equal(image[2, 2], nanResult[2, 2]);
        Assert.Equal(image[2, 3], nanResult[2, 3]);
    }

    /// <summary>
    /// Verifies that an empty matrix input produces an empty matrix output.
    /// </summary>
    [Fact]
    public void Apply_EmptyMatrix_ShouldReturnEmptyMatrix()
    {
        var emptyImage = new SequentialMatrix<Rgb8>(0, 0);
        var emptyOrientations = new SequentialMatrix<Vector2D>(0, 0);

        Matrix<Rgb8> result = OrientedEllipticalFilter.Apply(emptyImage, emptyOrientations);
        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that null inputs, mismatched matrix dimensions, and invalid options throw expected exceptions.
    /// </summary>
    [Fact]
    public void Apply_InvalidInputs_ShouldThrowAppropriateExceptions()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var orientations = new SequentialMatrix<Vector2D>(4, 4);
        var mismatchedOrientations = new SequentialMatrix<Vector2D>(5, 4);
        var mismatchedMask = new MaskMatrix(4, 5);

        Assert.Throws<ArgumentNullException>(() => OrientedEllipticalFilter.Apply(null!, orientations));
        Assert.Throws<ArgumentNullException>(() => OrientedEllipticalFilter.Apply(image, null!));

        Assert.Throws<ArgumentException>(() => OrientedEllipticalFilter.Apply(image, mismatchedOrientations));
        Assert.Throws<ArgumentException>(() => OrientedEllipticalFilter.Apply(image, orientations, null, mismatchedMask));

        var invalidOptions = new OrientedEllipticalFilterOptions(MajorRadius: -1.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => OrientedEllipticalFilter.Apply(image, orientations, invalidOptions));
    }
}

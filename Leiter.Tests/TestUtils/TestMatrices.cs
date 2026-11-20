namespace Leiter.Tests.TestUtils;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Provides factory helpers for constructing synthetic matrices and region graphs in tests.
/// </summary>
public static class TestMatrices
{
    /// <summary>
    /// Creates a <see cref="SequentialMatrix{T}"/> filled with a constant value.
    /// </summary>
    public static SequentialMatrix<T> Constant<T>(int width, int height, T value)
        where T : struct
    {
        var matrix = new SequentialMatrix<T>(width, height);
        matrix.SetAll(value);
        return matrix;
    }

    /// <summary>
    /// Creates a <see cref="SequentialMatrix{T}"/> populated by a coordinate generator.
    /// </summary>
    public static SequentialMatrix<T> Create<T>(int width, int height, Func<int, int, T> generator)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(generator);

        var matrix = new SequentialMatrix<T>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                matrix[x, y] = generator(x, y);
            }
        }

        return matrix;
    }

    /// <summary>
    /// Creates a <see cref="RegionGraph"/> containing a single uniform region spanning the specified dimensions.
    /// </summary>
    public static RegionGraph SingleRegionGraph(
        int width,
        int height,
        GridConnectivity connectivity = GridConnectivity.EightConnected) =>
        RegionGraph.FromLabels(new SequentialMatrix<int>(width, height), connectivity);

    /// <summary>
    /// Creates a <see cref="RegionGraph"/> containing a single uniform region spanning <paramref name="size"/>.
    /// </summary>
    public static RegionGraph SingleRegionGraph(
        Size size,
        GridConnectivity connectivity = GridConnectivity.EightConnected) =>
        SingleRegionGraph(size.Width, size.Height, connectivity);

    /// <summary>
    /// Creates an RGB image split vertically at <paramref name="splitX"/> into left and right colors.
    /// </summary>
    public static SequentialMatrix<Rgb8> VerticalSplit(
        int width,
        int height,
        Rgb8 left,
        Rgb8 right,
        int? splitX = null)
    {
        int split = splitX ?? (width / 2);
        return Create(width, height, (x, _) => x < split ? left : right);
    }

    /// <summary>
    /// Creates an RGB image split horizontally at <paramref name="splitY"/> into top and bottom colors.
    /// </summary>
    public static SequentialMatrix<Rgb8> HorizontalSplit(
        int width,
        int height,
        Rgb8 top,
        Rgb8 bottom,
        int? splitY = null)
    {
        int split = splitY ?? (height / 2);
        return Create(width, height, (_, y) => y < split ? top : bottom);
    }

    /// <summary>
    /// Creates a vertically split RGB image paired with its 2-region <see cref="RegionGraph"/>.
    /// </summary>
    public static (SequentialMatrix<Rgb8> Image, RegionGraph Graph) VerticalSplitWithGraph(
        int width,
        int height,
        Rgb8 left,
        Rgb8 right,
        int? splitX = null)
    {
        int split = splitX ?? (width / 2);
        SequentialMatrix<Rgb8> image = VerticalSplit(width, height, left, right, split);
        SequentialMatrix<int> labels = Create(width, height, (x, _) => x < split ? 0 : 1);
        return (image, RegionGraph.FromLabels(labels));
    }

    /// <summary>
    /// Creates a horizontally split RGB image paired with its 2-region <see cref="RegionGraph"/>.
    /// </summary>
    public static (SequentialMatrix<Rgb8> Image, RegionGraph Graph) HorizontalSplitWithGraph(
        int width,
        int height,
        Rgb8 top,
        Rgb8 bottom,
        int? splitY = null)
    {
        int split = splitY ?? (height / 2);
        SequentialMatrix<Rgb8> image = HorizontalSplit(width, height, top, bottom, split);
        SequentialMatrix<int> labels = Create(width, height, (_, y) => y < split ? 0 : 1);
        return (image, RegionGraph.FromLabels(labels));
    }

    /// <summary>
    /// Creates a matrix containing an axis-aligned rectangular box with inclusive bounds.
    /// </summary>
    public static SequentialMatrix<T> CenteredBox<T>(
        int width,
        int height,
        int left,
        int top,
        int rightInclusive,
        int bottomInclusive,
        T inside,
        T outside)
        where T : struct =>
        Create(
            width,
            height,
            (x, y) => x >= left && x <= rightInclusive && y >= top && y <= bottomInclusive ? inside : outside);

    /// <summary>
    /// Creates a smooth RGB gradient covering [0, 255] across width and height.
    /// </summary>
    public static SequentialMatrix<Rgb8> GradientRgb8(int width, int height)
    {
        int denomX = Math.Max(1, width - 1);
        int denomY = Math.Max(1, height - 1);
        return Create(
            width,
            height,
            (x, y) => new Rgb8(
                (byte)((x * 255) / denomX),
                (byte)((y * 255) / denomY),
                (byte)(((x + y) * 255) / Math.Max(1, denomX + denomY))));
    }

    /// <summary>
    /// Creates a checkerboard pattern of square cells with side length <paramref name="cellSize"/>.
    /// </summary>
    public static SequentialMatrix<Rgb8> Checkerboard(
        int width,
        int height,
        Rgb8 even,
        Rgb8 odd,
        int cellSize = 1) =>
        Create(
            width,
            height,
            (x, y) => (((x / cellSize) + (y / cellSize)) & 1) == 0 ? even : odd);
}

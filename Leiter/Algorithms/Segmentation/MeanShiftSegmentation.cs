namespace Leiter.Algorithms.Segmentation;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Provides Mean-Shift filtering and image segmentation algorithms operating in joint spatial and CIELAB color feature space.
/// </summary>
public static class MeanShiftSegmentation
{
    /// <summary>
    /// The spatial radius threshold above which the spatial-color lattice is used for mode seeking.
    /// </summary>
    internal const int LatticeRadiusThreshold = 16;

    /// <summary>
    /// Performs edge-preserving Mean-Shift smoothing on a CIELAB image.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to filter.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>A filtered <see cref="Matrix{Lab32}"/> containing the converged mode color at each pixel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static Matrix<Lab32> Filter(IReadOnlyMatrix<Lab32> labImage, MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(labImage);
        MeanShiftOptions resolvedOptions = ResolveOptions(options);

        if (labImage.Count == 0)
            return new SequentialMatrix<Lab32>(labImage.Width, labImage.Height);

        (MeanShiftMode[] modes, _) = MeanShiftModeSeeker.SeekModes(labImage, resolvedOptions, LatticeRadiusThreshold);
        return BuildFilteredLabMatrix(labImage.Width, labImage.Height, modes);
    }

    /// <summary>
    /// Performs edge-preserving Mean-Shift smoothing on an 8-bit sRGB image.
    /// </summary>
    /// <param name="image">The input sRGB image to filter.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>A filtered <see cref="Matrix{Rgb8}"/> containing the converged mode color at each pixel converted to sRGB.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static Matrix<Rgb8> Filter(IReadOnlyMatrix<Rgb8> image, MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        MeanShiftOptions resolvedOptions = ResolveOptions(options);

        if (image.Count == 0)
            return new SequentialMatrix<Rgb8>(image.Width, image.Height);

        Matrix<Lab32> labMatrix = image.Map(static pixel => pixel.ToLab32(RgbColorSpace.SRgb));
        (MeanShiftMode[] modes, _) = MeanShiftModeSeeker.SeekModes(labMatrix, resolvedOptions, LatticeRadiusThreshold);
        return BuildFilteredLabMatrix(image.Width, image.Height, modes).Map(static pixel => pixel.ToRgb8(RgbColorSpace.SRgb));
    }

    /// <summary>
    /// Simultaneously performs edge-preserving Mean-Shift smoothing and region segmentation on a CIELAB image,
    /// sharing a single mode-seeking pass.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to filter and segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>A tuple containing the filtered <see cref="Matrix{Lab32}"/> image and the partitioned <see cref="IDisjointSet"/> regions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static (Matrix<Lab32> FilteredImage, IDisjointSet Segmentation) FilterAndSegment(
        IReadOnlyMatrix<Lab32> labImage,
        MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(labImage);
        MeanShiftOptions resolvedOptions = ResolveOptions(options);

        if (labImage.Count == 0)
        {
            return (
                new SequentialMatrix<Lab32>(labImage.Width, labImage.Height),
                new DisjointSet<byte>(labImage, static _ => 0));
        }

        (MeanShiftMode[] modes, Lab32[] sourcePixels) = MeanShiftModeSeeker.SeekModes(labImage, resolvedOptions, LatticeRadiusThreshold);
        Matrix<Lab32> filtered = BuildFilteredLabMatrix(labImage.Width, labImage.Height, modes);
        IDisjointSet segmentation = MeanShiftRegionClusterer.ClusterModes(labImage, sourcePixels, modes, resolvedOptions);
        return (filtered, segmentation);
    }

    /// <summary>
    /// Simultaneously performs edge-preserving Mean-Shift smoothing and region segmentation on an 8-bit sRGB image,
    /// sharing a single mode-seeking pass.
    /// </summary>
    /// <param name="image">The input sRGB image to filter and segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>A tuple containing the filtered <see cref="Matrix{Rgb8}"/> image and the partitioned <see cref="IDisjointSet"/> regions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static (Matrix<Rgb8> FilteredImage, IDisjointSet Segmentation) FilterAndSegment(
        IReadOnlyMatrix<Rgb8> image,
        MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        MeanShiftOptions resolvedOptions = ResolveOptions(options);

        if (image.Count == 0)
        {
            return (
                new SequentialMatrix<Rgb8>(image.Width, image.Height),
                new DisjointSet<byte>(image, static _ => 0));
        }

        Matrix<Lab32> labMatrix = image.Map(static pixel => pixel.ToLab32(RgbColorSpace.SRgb));
        (MeanShiftMode[] modes, Lab32[] sourcePixels) = MeanShiftModeSeeker.SeekModes(labMatrix, resolvedOptions, LatticeRadiusThreshold);
        Matrix<Rgb8> filtered = BuildFilteredLabMatrix(image.Width, image.Height, modes).Map(static pixel => pixel.ToRgb8(RgbColorSpace.SRgb));
        IDisjointSet segmentation = MeanShiftRegionClusterer.ClusterModes(image, sourcePixels, modes, resolvedOptions);
        return (filtered, segmentation);
    }

    /// <summary>
    /// Segments a CIELAB image into disjoint sets of pixels using Mean-Shift mode clustering and region pruning.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>An <see cref="IDisjointSet"/> representing the partitioned regions of the image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static IDisjointSet Segment(IReadOnlyMatrix<Lab32> labImage, MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(labImage);
        MeanShiftOptions resolvedOptions = ResolveOptions(options);

        if (labImage.Count == 0)
            return new DisjointSet<byte>(labImage, static _ => 0);

        (MeanShiftMode[] modes, Lab32[] sourcePixels) = MeanShiftModeSeeker.SeekModes(labImage, resolvedOptions, LatticeRadiusThreshold);
        return MeanShiftRegionClusterer.ClusterModes(labImage, sourcePixels, modes, resolvedOptions);
    }

    /// <summary>
    /// Segments an 8-bit sRGB image into disjoint sets of pixels using Mean-Shift mode clustering and region pruning.
    /// </summary>
    /// <param name="image">The input sRGB image to segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>An <see cref="IDisjointSet"/> representing the partitioned regions of the image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static IDisjointSet Segment(IReadOnlyMatrix<Rgb8> image, MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        MeanShiftOptions resolvedOptions = ResolveOptions(options);

        if (image.Count == 0)
            return new DisjointSet<byte>(image, static _ => 0);

        Matrix<Lab32> labMatrix = image.Map(static pixel => pixel.ToLab32(RgbColorSpace.SRgb));
        (MeanShiftMode[] modes, Lab32[] sourcePixels) = MeanShiftModeSeeker.SeekModes(labMatrix, resolvedOptions, LatticeRadiusThreshold);
        return MeanShiftRegionClusterer.ClusterModes(image, sourcePixels, modes, resolvedOptions);
    }

    private static MeanShiftOptions ResolveOptions(MeanShiftOptions? options)
    {
        MeanShiftOptions resolved = options ?? MeanShiftOptions.Default;
        resolved.Validate();
        return resolved;
    }

    private static Matrix<Lab32> BuildFilteredLabMatrix(int width, int height, MeanShiftMode[] modes)
    {
        var resultData = new Lab32[modes.Length];
        for (int i = 0; i < resultData.Length; i++)
        {
            resultData[i] = modes[i].Color;
        }

        return new SequentialMatrix<Lab32>(width, height, resultData);
    }
}

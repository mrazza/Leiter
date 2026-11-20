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
        MeanShiftOptions resolvedOptions = OptionsResolver.Resolve(options);
        return Run(labImage, resolvedOptions, wantFilter: true, wantSegments: false).Filtered!;
    }

    /// <summary>
    /// Performs edge-preserving Mean-Shift smoothing on an 8-bit sRGB image.
    /// </summary>
    /// <param name="image">The input sRGB image to filter.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>A filtered <see cref="Matrix{Rgb8}"/> containing the converged mode color at each pixel converted to sRGB.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static Matrix<Rgb8> Filter(IReadOnlyMatrix<Rgb8> image, MeanShiftOptions? options = null) =>
        RunRgb(image, options, wantFilter: true, wantSegments: false).Filtered!;

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
        MeanShiftOptions resolvedOptions = OptionsResolver.Resolve(options);
        (Matrix<Lab32>? filtered, IDisjointSet? segmentation) = Run(labImage, resolvedOptions, wantFilter: true, wantSegments: true);
        return (filtered!, segmentation!);
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
        (Matrix<Rgb8>? filtered, IDisjointSet? segmentation) = RunRgb(image, options, wantFilter: true, wantSegments: true);
        return (filtered!, segmentation!);
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
        MeanShiftOptions resolvedOptions = OptionsResolver.Resolve(options);
        return Run(labImage, resolvedOptions, wantFilter: false, wantSegments: true).Segmentation!;
    }

    /// <summary>
    /// Segments an 8-bit sRGB image into disjoint sets of pixels using Mean-Shift mode clustering and region pruning.
    /// </summary>
    /// <param name="image">The input sRGB image to segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>An <see cref="IDisjointSet"/> representing the partitioned regions of the image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static IDisjointSet Segment(IReadOnlyMatrix<Rgb8> image, MeanShiftOptions? options = null) =>
        RunRgb(image, options, wantFilter: false, wantSegments: true).Segmentation!;

    private static (Matrix<Rgb8>? Filtered, IDisjointSet? Segmentation) RunRgb(
        IReadOnlyMatrix<Rgb8> image,
        MeanShiftOptions? options,
        bool wantFilter,
        bool wantSegments)
    {
        ArgumentNullException.ThrowIfNull(image);
        MeanShiftOptions resolvedOptions = OptionsResolver.Resolve(options);

        if (image.Count == 0)
        {
            return (
                wantFilter ? new SequentialMatrix<Rgb8>(image.Size) : null,
                wantSegments ? DisjointSet.CreateSingletons(image) : null);
        }

        Matrix<Lab32> labMatrix = image.ToLab32(RgbColorSpace.SRgb);
        (Matrix<Lab32>? labFiltered, IDisjointSet? segmentation) = Run(labMatrix, resolvedOptions, wantFilter, wantSegments);
        return (labFiltered?.ToRgb8(RgbColorSpace.SRgb), segmentation);
    }

    private static (Matrix<Lab32>? Filtered, IDisjointSet? Segmentation) Run(
        IReadOnlyMatrix<Lab32> lab,
        MeanShiftOptions opts,
        bool wantFilter,
        bool wantSegments)
    {
        if (lab.Count == 0)
        {
            return (
                wantFilter ? new SequentialMatrix<Lab32>(lab.Size) : null,
                wantSegments ? DisjointSet.CreateSingletons(lab) : null);
        }

        (MeanShiftMode[] modes, Lab32[] sourcePixels) = MeanShiftModeSeeker.SeekModes(lab, opts);
        Matrix<Lab32>? filtered = wantFilter ? BuildFilteredLabMatrix(lab.Size, modes) : null;
        IDisjointSet? segmentation = wantSegments
            ? MeanShiftRegionClusterer.ClusterModes(lab, sourcePixels, modes, opts)
            : null;
        return (filtered, segmentation);
    }

    private static Matrix<Lab32> BuildFilteredLabMatrix(Size size, MeanShiftMode[] modes)
    {
        var resultData = new Lab32[modes.Length];
        for (int i = 0; i < resultData.Length; i++)
        {
            resultData[i] = modes[i].Color;
        }

        return new SequentialMatrix<Lab32>(size.Width, size.Height, resultData);
    }
}

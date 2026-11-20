namespace Leiter.Algorithms.Segmentation;

using System;
using System.Threading;
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
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A filtered <see cref="Matrix{Lab32}"/> containing the converged mode color at each pixel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Matrix<Lab32> Filter(
        IReadOnlyMatrix<Lab32> labImage,
        MeanShiftOptions? options = null,
        CancellationToken cancellationToken = default) =>
        SeekModesAndFilter(labImage, options, cancellationToken).FilteredImage;

    /// <summary>
    /// Performs edge-preserving Mean-Shift smoothing on an 8-bit sRGB image.
    /// </summary>
    /// <param name="image">The input sRGB image to filter.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A filtered <see cref="Matrix{Rgb8}"/> containing the converged mode color at each pixel converted to sRGB.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Matrix<Rgb8> Filter(
        IReadOnlyMatrix<Rgb8> image,
        MeanShiftOptions? options = null,
        CancellationToken cancellationToken = default) =>
        SeekModesAndFilter(image, options, cancellationToken).FilteredImage;

    /// <summary>
    /// Simultaneously performs edge-preserving Mean-Shift smoothing and region segmentation on a CIELAB image,
    /// sharing a single mode-seeking pass.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to filter and segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple containing the filtered <see cref="Matrix{Lab32}"/> image and the partitioned <see cref="IDisjointSet"/> regions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static (Matrix<Lab32> FilteredImage, IDisjointSet Segmentation) FilterAndSegment(
        IReadOnlyMatrix<Lab32> labImage,
        MeanShiftOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        (MeanShiftMode[] modes, Lab32[] sourcePixels, Matrix<Lab32> filtered) =
            SeekModesAndFilter(labImage, options, cancellationToken);
        IDisjointSet segmentation = ClusterModes(labImage, sourcePixels, modes, options, cancellationToken);
        return (filtered, segmentation);
    }

    /// <summary>
    /// Simultaneously performs edge-preserving Mean-Shift smoothing and region segmentation on an 8-bit sRGB image,
    /// sharing a single mode-seeking pass.
    /// </summary>
    /// <param name="image">The input sRGB image to filter and segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple containing the filtered <see cref="Matrix{Rgb8}"/> image and the partitioned <see cref="IDisjointSet"/> regions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static (Matrix<Rgb8> FilteredImage, IDisjointSet Segmentation) FilterAndSegment(
        IReadOnlyMatrix<Rgb8> image,
        MeanShiftOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        (MeanShiftMode[] modes, Lab32[] sourcePixels, Matrix<Rgb8> filtered) =
            SeekModesAndFilter(image, options, cancellationToken);
        IDisjointSet segmentation = ClusterModes(filtered, sourcePixels, modes, options, cancellationToken);
        return (filtered, segmentation);
    }

    /// <summary>
    /// Segments a CIELAB image into disjoint sets of pixels using Mean-Shift mode clustering and region pruning.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="IDisjointSet"/> representing the partitioned regions of the image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static IDisjointSet Segment(
        IReadOnlyMatrix<Lab32> labImage,
        MeanShiftOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(labImage);
        MeanShiftOptions resolvedOptions = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (labImage.Count == 0)
            return DisjointSet.CreateSingletons(labImage);

        (MeanShiftMode[] modes, Lab32[] sourcePixels) =
            MeanShiftModeSeeker.SeekModes(labImage, resolvedOptions, cancellationToken: cancellationToken);
        return MeanShiftRegionClusterer.ClusterModes(labImage, sourcePixels, modes, resolvedOptions, cancellationToken);
    }

    /// <summary>
    /// Segments an 8-bit sRGB image into disjoint sets of pixels using Mean-Shift mode clustering and region pruning.
    /// </summary>
    /// <param name="image">The input sRGB image to segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="IDisjointSet"/> representing the partitioned regions of the image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static IDisjointSet Segment(
        IReadOnlyMatrix<Rgb8> image,
        MeanShiftOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        MeanShiftOptions resolvedOptions = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0)
            return DisjointSet.CreateSingletons(image);

        Matrix<Lab32> labMatrix = image.ToLab32(RgbColorSpace.SRgb);
        cancellationToken.ThrowIfCancellationRequested();

        (MeanShiftMode[] modes, Lab32[] sourcePixels) =
            MeanShiftModeSeeker.SeekModes(labMatrix, resolvedOptions, cancellationToken: cancellationToken);
        return MeanShiftRegionClusterer.ClusterModes(labMatrix, sourcePixels, modes, resolvedOptions, cancellationToken);
    }

    /// <summary>
    /// Performs the 5D Mean-Shift mode-seeking pass on an 8-bit sRGB image and builds the filtered sRGB output,
    /// returning the converged modes and CIELAB source pixels so region clustering can be cached and re-executed separately.
    /// </summary>
    internal static (MeanShiftMode[] Modes, Lab32[] SourcePixels, Matrix<Rgb8> FilteredImage) SeekModesAndFilter(
        IReadOnlyMatrix<Rgb8> image,
        MeanShiftOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        MeanShiftOptions resolvedOptions = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0)
        {
            return ([], [], new SequentialMatrix<Rgb8>(image.Size));
        }

        Matrix<Lab32> labMatrix = image.ToLab32(RgbColorSpace.SRgb);
        cancellationToken.ThrowIfCancellationRequested();

        (MeanShiftMode[] modes, Lab32[] sourcePixels, Matrix<Lab32> filteredLab) =
            SeekModesAndFilterCore(labMatrix, resolvedOptions, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return (modes, sourcePixels, filteredLab.ToRgb8(RgbColorSpace.SRgb));
    }

    /// <summary>
    /// Performs the 5D Mean-Shift mode-seeking pass on a CIELAB image and builds the filtered CIELAB output,
    /// returning the converged modes and flat CIELAB source pixels so region clustering can be cached and re-executed separately.
    /// </summary>
    internal static (MeanShiftMode[] Modes, Lab32[] SourcePixels, Matrix<Lab32> FilteredImage) SeekModesAndFilter(
        IReadOnlyMatrix<Lab32> labImage,
        MeanShiftOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(labImage);
        MeanShiftOptions resolvedOptions = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (labImage.Count == 0)
        {
            return ([], [], new SequentialMatrix<Lab32>(labImage.Size));
        }

        return SeekModesAndFilterCore(labImage, resolvedOptions, cancellationToken);
    }

    /// <summary>
    /// Clusters previously converged 5D Mean-Shift modes into disjoint pixel regions using the clustering parameters in <paramref name="options"/>.
    /// </summary>
    internal static IDisjointSet ClusterModes(
        IUntypedMatrix targetImage,
        Lab32[] sourcePixels,
        MeanShiftMode[] modes,
        MeanShiftOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetImage);
        MeanShiftOptions resolvedOptions = OptionsResolver.Resolve(options);
        return MeanShiftRegionClusterer.ClusterModes(targetImage, sourcePixels, modes, resolvedOptions, cancellationToken);
    }

    private static (MeanShiftMode[] Modes, Lab32[] SourcePixels, Matrix<Lab32> FilteredImage) SeekModesAndFilterCore(
        IReadOnlyMatrix<Lab32> lab,
        MeanShiftOptions resolvedOptions,
        CancellationToken cancellationToken)
    {
        (MeanShiftMode[] modes, Lab32[] sourcePixels) =
            MeanShiftModeSeeker.SeekModes(lab, resolvedOptions, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        Matrix<Lab32> filtered = BuildFilteredLabMatrix(lab.Size, modes);
        return (modes, sourcePixels, filtered);
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

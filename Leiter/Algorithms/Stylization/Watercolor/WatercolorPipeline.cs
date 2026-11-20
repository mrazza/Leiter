namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Threading;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Orchestrates the complete Wang et al. (IEEE TVCG 2014) photo watercolorization pipeline across Stages 1–6.
/// </summary>
public static class WatercolorPipeline
{
    /// <summary>
    /// Executes the full watercolorization pipeline on <paramref name="image"/> and returns the final painting alongside intermediate stage outputs.
    /// </summary>
    /// <param name="image">The input RGB photograph to watercolorize.</param>
    /// <param name="paperHeightMap">Optional custom normalized [0, 1] paper height map used during Stage 6 granulation.</param>
    /// <param name="options">Optional pipeline configuration parameters controlling each stage.</param>
    /// <param name="onStageCompleted">Optional callback invoked as each major pipeline stage completes (not invoked when <paramref name="image"/> is empty).</param>
    /// <param name="cancellationToken">Optional token used to cancel execution cooperatively between and within pipeline steps.</param>
    /// <returns>A <see cref="WatercolorResult"/> containing the final rendered painting and stage intermediates.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="paperHeightMap"/> dimensions do not match <paramref name="image"/> or <paramref name="options"/> contains an invalid palette name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> contains invalid parameters.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static WatercolorResult RenderDetailed(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel>? paperHeightMap = null,
        WatercolorOptions? options = null,
        Action<WatercolorStageProgress>? onStageCompleted = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        WatercolorOptions resolvedOptions = OptionsResolver.Resolve(options);
        Guard.ThrowIfSizeMismatchIfNotNull(image, paperHeightMap);

        var session = new WatercolorPipelineSession(image, paperHeightMap);
        return session.RenderCore(
            resolvedOptions,
            onStepCompleted: null,
            onStageCompleted,
            cancellationToken);
    }

    /// <summary>
    /// Executes the full watercolorization pipeline on <paramref name="image"/> and returns the final rendered watercolor painting.
    /// </summary>
    /// <param name="image">The input RGB photograph to watercolorize.</param>
    /// <param name="paperHeightMap">Optional custom normalized [0, 1] paper height map used during Stage 6 granulation.</param>
    /// <param name="options">Optional pipeline configuration parameters controlling each stage.</param>
    /// <param name="onStageCompleted">Optional callback invoked as each major pipeline stage completes (not invoked when <paramref name="image"/> is empty).</param>
    /// <param name="cancellationToken">Optional token used to cancel execution cooperatively between and within pipeline steps.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the final rendered watercolor painting.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="paperHeightMap"/> dimensions do not match <paramref name="image"/> or <paramref name="options"/> contains an invalid palette name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> contains invalid parameters.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<Rgb8> Render(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel>? paperHeightMap = null,
        WatercolorOptions? options = null,
        Action<WatercolorStageProgress>? onStageCompleted = null,
        CancellationToken cancellationToken = default) =>
        RenderDetailed(image, paperHeightMap, options, onStageCompleted, cancellationToken).Image;
}

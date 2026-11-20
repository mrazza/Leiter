namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Captures the final rendered watercolor painting alongside key intermediate outputs from Stages 1–6.
/// </summary>
public sealed record WatercolorResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WatercolorResult"/> record.
    /// </summary>
    /// <param name="image">The final rendered watercolor painting matrix.</param>
    /// <param name="colorTransferred">The Stage 1 Reinhard color-transferred image.</param>
    /// <param name="selectedPalette">The watercolor palette applied during Stage 1.</param>
    /// <param name="saliencyMap">The Stage 2 continuous regional contrast saliency map.</param>
    /// <param name="salientMask">The Stage 2 extracted binary salient region mask.</param>
    /// <param name="saliencyDistanceField">The Stage 2 normalized and smoothed [0, 1] saliency distance field.</param>
    /// <param name="segmentationGraph">The Stage 3 Mean-Shift region adjacency graph.</param>
    /// <param name="lodAbstracted">The Stage 3 saliency-guided LOD abstracted image.</param>
    /// <param name="wetInWet">The Stage 4 wet-in-wet detailed output.</param>
    /// <param name="handTremor">The Stage 5 hand-tremor perturbed image.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required output artifact is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when matrix or region graph dimensions or boundary counts are inconsistent with <paramref name="image"/>.</exception>
    public WatercolorResult(
        Matrix<Rgb8> image,
        Matrix<Rgb8> colorTransferred,
        ColorTransferProfile selectedPalette,
        Matrix<DoublePixel> saliencyMap,
        MaskMatrix salientMask,
        Matrix<DoublePixel> saliencyDistanceField,
        RegionGraph segmentationGraph,
        Matrix<Rgb8> lodAbstracted,
        WetInWetResult wetInWet,
        Matrix<Rgb8> handTremor)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(colorTransferred);
        ArgumentNullException.ThrowIfNull(selectedPalette);
        ArgumentNullException.ThrowIfNull(saliencyMap);
        ArgumentNullException.ThrowIfNull(salientMask);
        ArgumentNullException.ThrowIfNull(saliencyDistanceField);
        ArgumentNullException.ThrowIfNull(segmentationGraph);
        ArgumentNullException.ThrowIfNull(lodAbstracted);
        ArgumentNullException.ThrowIfNull(wetInWet);
        ArgumentNullException.ThrowIfNull(handTremor);

        Guard.ThrowIfSizeMismatch(image, colorTransferred);
        Guard.ThrowIfSizeMismatch(image, saliencyMap);
        Guard.ThrowIfSizeMismatch(image, salientMask);
        Guard.ThrowIfSizeMismatch(image, saliencyDistanceField);
        Guard.ThrowIfSizeMismatch(image, lodAbstracted);
        Guard.ThrowIfSizeMismatch(image, wetInWet.Image, nameof(wetInWet));
        Guard.ThrowIfSizeMismatch(image, handTremor);
        Guard.ThrowIfSizeMismatch(image.Size, segmentationGraph.Size, nameof(segmentationGraph));

        if (wetInWet.QualifyingBoundaries.Count != segmentationGraph.Boundaries.Count)
        {
            throw new ArgumentException(
                $"Qualifying boundary count ({wetInWet.QualifyingBoundaries.Count}) must match segmentation graph boundary count ({segmentationGraph.Boundaries.Count}).",
                nameof(wetInWet));
        }

        Image = image;
        ColorTransferred = colorTransferred;
        SelectedPalette = selectedPalette;
        SaliencyMap = saliencyMap;
        SalientMask = salientMask;
        SaliencyDistanceField = saliencyDistanceField;
        SegmentationGraph = segmentationGraph;
        LodAbstracted = lodAbstracted;
        WetInWet = wetInWet;
        HandTremor = handTremor;
    }

    /// <summary>
    /// Gets the final rendered watercolor painting matrix.
    /// </summary>
    public Matrix<Rgb8> Image { get; init; }

    /// <summary>
    /// Gets the Stage 1 Reinhard color-transferred image.
    /// </summary>
    public Matrix<Rgb8> ColorTransferred { get; init; }

    /// <summary>
    /// Gets the watercolor palette applied during Stage 1.
    /// </summary>
    public ColorTransferProfile SelectedPalette { get; init; }

    /// <summary>
    /// Gets the Stage 2 continuous regional contrast saliency map.
    /// </summary>
    public Matrix<DoublePixel> SaliencyMap { get; init; }

    /// <summary>
    /// Gets the Stage 2 extracted binary salient region mask.
    /// </summary>
    public MaskMatrix SalientMask { get; init; }

    /// <summary>
    /// Gets the Stage 2 normalized and smoothed [0, 1] saliency distance field.
    /// </summary>
    public Matrix<DoublePixel> SaliencyDistanceField { get; init; }

    /// <summary>
    /// Gets the Stage 3 Mean-Shift region adjacency graph.
    /// </summary>
    public RegionGraph SegmentationGraph { get; init; }

    /// <summary>
    /// Gets the Stage 3 saliency-guided LOD abstracted image.
    /// </summary>
    public Matrix<Rgb8> LodAbstracted { get; init; }

    /// <summary>
    /// Gets the Stage 4 wet-in-wet detailed output.
    /// </summary>
    public WetInWetResult WetInWet { get; init; }

    /// <summary>
    /// Gets the Stage 5 hand-tremor perturbed image.
    /// </summary>
    public Matrix<Rgb8> HandTremor { get; init; }
}

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
    /// <param name="meanShiftFiltered">The Stage 3a Mean-Shift filtered image.</param>
    /// <param name="segmentationGraph">The Stage 3a Mean-Shift region adjacency graph.</param>
    /// <param name="lodAbstracted">The Stage 3b saliency-guided LOD abstracted image.</param>
    /// <param name="wetInWet">The Stage 4 wet-in-wet detailed output.</param>
    /// <param name="handTremor">The Stage 5 hand-tremor perturbed image.</param>
    /// <param name="densityAdjusted">The Stage 6a pigment-density adjusted image after edge darkening and turbulence flow.</param>
    /// <param name="toneAdjusted">The Stage 6b piecewise tone-curve adjusted image.</param>
    /// <param name="granulated">The Stage 6c paper-granulated image prior to optional FXAA.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required output artifact is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when matrix or region graph dimensions or boundary counts are inconsistent with <paramref name="image"/>.</exception>
    public WatercolorResult(
        Matrix<Rgb8> image,
        Matrix<Rgb8> colorTransferred,
        ColorTransferProfile selectedPalette,
        Matrix<DoublePixel> saliencyMap,
        MaskMatrix salientMask,
        Matrix<DoublePixel> saliencyDistanceField,
        Matrix<Rgb8> meanShiftFiltered,
        RegionGraph segmentationGraph,
        Matrix<Rgb8> lodAbstracted,
        WetInWetResult wetInWet,
        Matrix<Rgb8> handTremor,
        Matrix<Rgb8> densityAdjusted,
        Matrix<Rgb8> toneAdjusted,
        Matrix<Rgb8> granulated)
    {
        Guard.ThrowIfSizeMismatch(image, colorTransferred);
        ArgumentNullException.ThrowIfNull(selectedPalette);
        Guard.ThrowIfSizeMismatch(image, saliencyMap);
        Guard.ThrowIfSizeMismatch(image, salientMask);
        Guard.ThrowIfSizeMismatch(image, saliencyDistanceField);
        Guard.ThrowIfSizeMismatch(image, meanShiftFiltered);
        ArgumentNullException.ThrowIfNull(segmentationGraph);
        Guard.ThrowIfSizeMismatch(image.Size, segmentationGraph.Size, nameof(segmentationGraph));
        Guard.ThrowIfSizeMismatch(image, lodAbstracted);
        ArgumentNullException.ThrowIfNull(wetInWet);
        wetInWet.ValidateAgainst(segmentationGraph, nameof(wetInWet));
        Guard.ThrowIfSizeMismatch(image, handTremor);
        Guard.ThrowIfSizeMismatch(image, densityAdjusted);
        Guard.ThrowIfSizeMismatch(image, toneAdjusted);
        Guard.ThrowIfSizeMismatch(image, granulated);

        Image = image;
        ColorTransferred = colorTransferred;
        SelectedPalette = selectedPalette;
        SaliencyMap = saliencyMap;
        SalientMask = salientMask;
        SaliencyDistanceField = saliencyDistanceField;
        MeanShiftFiltered = meanShiftFiltered;
        SegmentationGraph = segmentationGraph;
        LodAbstracted = lodAbstracted;
        WetInWet = wetInWet;
        HandTremor = handTremor;
        DensityAdjusted = densityAdjusted;
        ToneAdjusted = toneAdjusted;
        Granulated = granulated;
    }

    /// <summary>
    /// Gets the final rendered watercolor painting matrix.
    /// </summary>
    public Matrix<Rgb8> Image { get; }

    /// <summary>
    /// Gets the Stage 1 Reinhard color-transferred image.
    /// </summary>
    public Matrix<Rgb8> ColorTransferred { get; }

    /// <summary>
    /// Gets the watercolor palette applied during Stage 1.
    /// </summary>
    public ColorTransferProfile SelectedPalette { get; }

    /// <summary>
    /// Gets the Stage 2 continuous regional contrast saliency map.
    /// </summary>
    public Matrix<DoublePixel> SaliencyMap { get; }

    /// <summary>
    /// Gets the Stage 2 extracted binary salient region mask.
    /// </summary>
    public MaskMatrix SalientMask { get; }

    /// <summary>
    /// Gets the Stage 2 normalized and smoothed [0, 1] saliency distance field.
    /// </summary>
    public Matrix<DoublePixel> SaliencyDistanceField { get; }

    /// <summary>
    /// Gets the Stage 3a Mean-Shift filtered image.
    /// </summary>
    public Matrix<Rgb8> MeanShiftFiltered { get; }

    /// <summary>
    /// Gets the Stage 3a Mean-Shift region adjacency graph.
    /// </summary>
    public RegionGraph SegmentationGraph { get; }

    /// <summary>
    /// Gets the Stage 3b saliency-guided LOD abstracted image.
    /// </summary>
    public Matrix<Rgb8> LodAbstracted { get; }

    /// <summary>
    /// Gets the Stage 4 wet-in-wet detailed output.
    /// </summary>
    public WetInWetResult WetInWet { get; }

    /// <summary>
    /// Gets the Stage 5 hand-tremor perturbed image.
    /// </summary>
    public Matrix<Rgb8> HandTremor { get; }

    /// <summary>
    /// Gets the Stage 6a pigment-density adjusted image after edge darkening and turbulence flow.
    /// </summary>
    public Matrix<Rgb8> DensityAdjusted { get; }

    /// <summary>
    /// Gets the Stage 6b piecewise tone-curve adjusted image.
    /// </summary>
    public Matrix<Rgb8> ToneAdjusted { get; }

    /// <summary>
    /// Gets the Stage 6c paper-granulated image prior to optional FXAA.
    /// </summary>
    public Matrix<Rgb8> Granulated { get; }

    /// <summary>
    /// Deconstructs this result into its constituent final and intermediate outputs.
    /// </summary>
    /// <param name="image">The final rendered watercolor painting matrix.</param>
    /// <param name="colorTransferred">The Stage 1 Reinhard color-transferred image.</param>
    /// <param name="selectedPalette">The watercolor palette applied during Stage 1.</param>
    /// <param name="saliencyMap">The Stage 2 continuous regional contrast saliency map.</param>
    /// <param name="salientMask">The Stage 2 extracted binary salient region mask.</param>
    /// <param name="saliencyDistanceField">The Stage 2 normalized and smoothed saliency distance field.</param>
    /// <param name="meanShiftFiltered">The Stage 3a Mean-Shift filtered image.</param>
    /// <param name="segmentationGraph">The Stage 3a Mean-Shift region adjacency graph.</param>
    /// <param name="lodAbstracted">The Stage 3b saliency-guided LOD abstracted image.</param>
    /// <param name="wetInWet">The Stage 4 wet-in-wet detailed output.</param>
    /// <param name="handTremor">The Stage 5 hand-tremor perturbed image.</param>
    /// <param name="densityAdjusted">The Stage 6a pigment-density adjusted image after edge darkening and turbulence flow.</param>
    /// <param name="toneAdjusted">The Stage 6b piecewise tone-curve adjusted image.</param>
    /// <param name="granulated">The Stage 6c paper-granulated image prior to optional FXAA.</param>
    public void Deconstruct(
        out Matrix<Rgb8> image,
        out Matrix<Rgb8> colorTransferred,
        out ColorTransferProfile selectedPalette,
        out Matrix<DoublePixel> saliencyMap,
        out MaskMatrix salientMask,
        out Matrix<DoublePixel> saliencyDistanceField,
        out Matrix<Rgb8> meanShiftFiltered,
        out RegionGraph segmentationGraph,
        out Matrix<Rgb8> lodAbstracted,
        out WetInWetResult wetInWet,
        out Matrix<Rgb8> handTremor,
        out Matrix<Rgb8> densityAdjusted,
        out Matrix<Rgb8> toneAdjusted,
        out Matrix<Rgb8> granulated)
    {
        image = Image;
        colorTransferred = ColorTransferred;
        selectedPalette = SelectedPalette;
        saliencyMap = SaliencyMap;
        salientMask = SalientMask;
        saliencyDistanceField = SaliencyDistanceField;
        meanShiftFiltered = MeanShiftFiltered;
        segmentationGraph = SegmentationGraph;
        lodAbstracted = LodAbstracted;
        wetInWet = WetInWet;
        handTremor = HandTremor;
        densityAdjusted = DensityAdjusted;
        toneAdjusted = ToneAdjusted;
        granulated = Granulated;
    }
}

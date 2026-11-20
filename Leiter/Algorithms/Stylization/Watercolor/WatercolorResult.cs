namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Captures the final rendered watercolor painting alongside intermediate outputs and step timings from Stages 1–6.
/// </summary>
public sealed record WatercolorResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WatercolorResult"/> record.
    /// </summary>
    /// <param name="image">The final rendered watercolor painting matrix.</param>
    /// <param name="colorTransferred">The Stage 1 Reinhard color-transferred image.</param>
    /// <param name="selectedPalette">The watercolor palette applied or matched during Stage 1.</param>
    /// <param name="egbiPreBlurred">The Stage 2a Gaussian pre-blurred image fed into EGBI over-segmentation.</param>
    /// <param name="egbiGraph">The Stage 2b EGBI over-segmentation region adjacency graph.</param>
    /// <param name="saliencyMap">The Stage 2c continuous regional contrast saliency map.</param>
    /// <param name="salientMask">The Stage 2d extracted binary salient region mask.</param>
    /// <param name="rawSaliencyDistanceField">The Stage 2e unsmoothed normalized [0, 1] saliency distance field.</param>
    /// <param name="saliencyDistanceField">The Stage 2f normalized and smoothed [0, 1] saliency distance field.</param>
    /// <param name="meanShiftFiltered">The Stage 3a Mean-Shift filtered image.</param>
    /// <param name="segmentationGraph">The Stage 3a Mean-Shift region adjacency graph.</param>
    /// <param name="lodAbstracted">The Stage 3b saliency-guided LOD abstracted image.</param>
    /// <param name="wetInWet">The Stage 4 wet-in-wet detailed output.</param>
    /// <param name="handTremor">The Stage 5 hand-tremor perturbed image.</param>
    /// <param name="edgeDensityField">The optional Stage 6a pigment edge-darkening density field, or <see langword="null"/> when disabled or zero intensity.</param>
    /// <param name="turbulenceDensityField">The optional Stage 6a turbulence flow density field, or <see langword="null"/> when disabled or zero intensity.</param>
    /// <param name="densityAdjusted">The Stage 6a pigment-density adjusted image after edge darkening and turbulence flow.</param>
    /// <param name="toneAdjusted">The Stage 6b piecewise tone-curve adjusted image.</param>
    /// <param name="paperHeightMap">The optional Stage 6c paper surface height map used during granulation, or <see langword="null"/> when granulation is bypassed.</param>
    /// <param name="granulated">The Stage 6c paper-granulated image prior to optional FXAA.</param>
    /// <param name="stepTimings">The per-step execution timing, cache, and bypass metadata.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required output artifact or <paramref name="stepTimings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when matrix or region graph dimensions or boundary counts are inconsistent with <paramref name="image"/>.</exception>
    public WatercolorResult(
        Matrix<Rgb8> image,
        Matrix<Rgb8> colorTransferred,
        ColorTransferProfile selectedPalette,
        Matrix<Rgb8> egbiPreBlurred,
        RegionGraph egbiGraph,
        Matrix<DoublePixel> saliencyMap,
        MaskMatrix salientMask,
        Matrix<DoublePixel> rawSaliencyDistanceField,
        Matrix<DoublePixel> saliencyDistanceField,
        Matrix<Rgb8> meanShiftFiltered,
        RegionGraph segmentationGraph,
        Matrix<Rgb8> lodAbstracted,
        WetInWetResult wetInWet,
        Matrix<Rgb8> handTremor,
        Matrix<DoublePixel>? edgeDensityField,
        Matrix<DoublePixel>? turbulenceDensityField,
        Matrix<Rgb8> densityAdjusted,
        Matrix<Rgb8> toneAdjusted,
        IReadOnlyMatrix<DoublePixel>? paperHeightMap,
        Matrix<Rgb8> granulated,
        IReadOnlyDictionary<WatercolorStep, WatercolorStepTiming> stepTimings)
    {
        Guard.ThrowIfSizeMismatch(image, colorTransferred);
        ArgumentNullException.ThrowIfNull(selectedPalette);
        Guard.ThrowIfSizeMismatch(image, egbiPreBlurred);
        ArgumentNullException.ThrowIfNull(egbiGraph);
        Guard.ThrowIfSizeMismatch(image.Size, egbiGraph.Size, nameof(egbiGraph));
        Guard.ThrowIfSizeMismatch(image, saliencyMap);
        Guard.ThrowIfSizeMismatch(image, salientMask);
        Guard.ThrowIfSizeMismatch(image, rawSaliencyDistanceField);
        Guard.ThrowIfSizeMismatch(image, saliencyDistanceField);
        Guard.ThrowIfSizeMismatch(image, meanShiftFiltered);
        ArgumentNullException.ThrowIfNull(segmentationGraph);
        Guard.ThrowIfSizeMismatch(image.Size, segmentationGraph.Size, nameof(segmentationGraph));
        Guard.ThrowIfSizeMismatch(image, lodAbstracted);
        ArgumentNullException.ThrowIfNull(wetInWet);
        wetInWet.ValidateAgainst(segmentationGraph, nameof(wetInWet));
        Guard.ThrowIfSizeMismatch(image, handTremor);
        Guard.ThrowIfSizeMismatchIfNotNull(image, edgeDensityField);
        Guard.ThrowIfSizeMismatchIfNotNull(image, turbulenceDensityField);
        Guard.ThrowIfSizeMismatch(image, densityAdjusted);
        Guard.ThrowIfSizeMismatch(image, toneAdjusted);
        Guard.ThrowIfSizeMismatchIfNotNull(image, paperHeightMap);
        Guard.ThrowIfSizeMismatch(image, granulated);
        ArgumentNullException.ThrowIfNull(stepTimings);

        Image = image;
        ColorTransferred = colorTransferred;
        SelectedPalette = selectedPalette;
        EgbiPreBlurred = egbiPreBlurred;
        EgbiGraph = egbiGraph;
        SaliencyMap = saliencyMap;
        SalientMask = salientMask;
        RawSaliencyDistanceField = rawSaliencyDistanceField;
        SaliencyDistanceField = saliencyDistanceField;
        MeanShiftFiltered = meanShiftFiltered;
        SegmentationGraph = segmentationGraph;
        LodAbstracted = lodAbstracted;
        WetInWet = wetInWet;
        HandTremor = handTremor;
        EdgeDensityField = edgeDensityField;
        TurbulenceDensityField = turbulenceDensityField;
        DensityAdjusted = densityAdjusted;
        ToneAdjusted = toneAdjusted;
        PaperHeightMap = paperHeightMap;
        Granulated = granulated;
        StepTimings = stepTimings.Count == 0
            ? ReadOnlyDictionary<WatercolorStep, WatercolorStepTiming>.Empty
            : new ReadOnlyDictionary<WatercolorStep, WatercolorStepTiming>(
                new Dictionary<WatercolorStep, WatercolorStepTiming>(stepTimings));
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
    /// Gets the watercolor palette applied or matched during Stage 1.
    /// </summary>
    public ColorTransferProfile SelectedPalette { get; }

    /// <summary>
    /// Gets the Stage 2a Gaussian pre-blurred image fed into EGBI over-segmentation.
    /// </summary>
    public Matrix<Rgb8> EgbiPreBlurred { get; }

    /// <summary>
    /// Gets the Stage 2b EGBI over-segmentation region adjacency graph.
    /// </summary>
    public RegionGraph EgbiGraph { get; }

    /// <summary>
    /// Gets the Stage 2c continuous regional contrast saliency map.
    /// </summary>
    public Matrix<DoublePixel> SaliencyMap { get; }

    /// <summary>
    /// Gets the Stage 2d extracted binary salient region mask.
    /// </summary>
    public MaskMatrix SalientMask { get; }

    /// <summary>
    /// Gets the Stage 2e unsmoothed normalized [0, 1] saliency distance field.
    /// </summary>
    public Matrix<DoublePixel> RawSaliencyDistanceField { get; }

    /// <summary>
    /// Gets the Stage 2f normalized and smoothed [0, 1] saliency distance field.
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
    /// Gets the optional Stage 6a pigment edge-darkening density field, or <see langword="null"/> when disabled or zero intensity.
    /// </summary>
    public Matrix<DoublePixel>? EdgeDensityField { get; }

    /// <summary>
    /// Gets the optional Stage 6a turbulence flow density field, or <see langword="null"/> when disabled or zero intensity.
    /// </summary>
    public Matrix<DoublePixel>? TurbulenceDensityField { get; }

    /// <summary>
    /// Gets the Stage 6a pigment-density adjusted image after edge darkening and turbulence flow.
    /// </summary>
    public Matrix<Rgb8> DensityAdjusted { get; }

    /// <summary>
    /// Gets the Stage 6b piecewise tone-curve adjusted image.
    /// </summary>
    public Matrix<Rgb8> ToneAdjusted { get; }

    /// <summary>
    /// Gets the optional Stage 6c paper surface height map used during granulation, or <see langword="null"/> when granulation is bypassed.
    /// </summary>
    public IReadOnlyMatrix<DoublePixel>? PaperHeightMap { get; }

    /// <summary>
    /// Gets the Stage 6c paper-granulated image prior to optional FXAA.
    /// </summary>
    public Matrix<Rgb8> Granulated { get; }

    /// <summary>
    /// Gets the read-only dictionary mapping each executed <see cref="WatercolorStep"/> to its timing, cache, and bypass metadata.
    /// </summary>
    public IReadOnlyDictionary<WatercolorStep, WatercolorStepTiming> StepTimings { get; }

    /// <summary>
    /// Deconstructs this result into its constituent final outputs, intermediate artifacts, and step timings.
    /// </summary>
    /// <param name="image">The final rendered watercolor painting matrix.</param>
    /// <param name="colorTransferred">The Stage 1 Reinhard color-transferred image.</param>
    /// <param name="selectedPalette">The watercolor palette applied or matched during Stage 1.</param>
    /// <param name="egbiPreBlurred">The Stage 2a Gaussian pre-blurred image fed into EGBI over-segmentation.</param>
    /// <param name="egbiGraph">The Stage 2b EGBI over-segmentation region adjacency graph.</param>
    /// <param name="saliencyMap">The Stage 2c continuous regional contrast saliency map.</param>
    /// <param name="salientMask">The Stage 2d extracted binary salient region mask.</param>
    /// <param name="rawSaliencyDistanceField">The Stage 2e unsmoothed normalized [0, 1] saliency distance field.</param>
    /// <param name="saliencyDistanceField">The Stage 2f normalized and smoothed saliency distance field.</param>
    /// <param name="meanShiftFiltered">The Stage 3a Mean-Shift filtered image.</param>
    /// <param name="segmentationGraph">The Stage 3a Mean-Shift region adjacency graph.</param>
    /// <param name="lodAbstracted">The Stage 3b saliency-guided LOD abstracted image.</param>
    /// <param name="wetInWet">The Stage 4 wet-in-wet detailed output.</param>
    /// <param name="handTremor">The Stage 5 hand-tremor perturbed image.</param>
    /// <param name="edgeDensityField">The optional Stage 6a pigment edge-darkening density field.</param>
    /// <param name="turbulenceDensityField">The optional Stage 6a turbulence flow density field.</param>
    /// <param name="densityAdjusted">The Stage 6a pigment-density adjusted image after edge darkening and turbulence flow.</param>
    /// <param name="toneAdjusted">The Stage 6b piecewise tone-curve adjusted image.</param>
    /// <param name="paperHeightMap">The optional Stage 6c paper surface height map used during granulation.</param>
    /// <param name="granulated">The Stage 6c paper-granulated image prior to optional FXAA.</param>
    /// <param name="stepTimings">The per-step execution timing, cache, and bypass metadata.</param>
    public void Deconstruct(
        out Matrix<Rgb8> image,
        out Matrix<Rgb8> colorTransferred,
        out ColorTransferProfile selectedPalette,
        out Matrix<Rgb8> egbiPreBlurred,
        out RegionGraph egbiGraph,
        out Matrix<DoublePixel> saliencyMap,
        out MaskMatrix salientMask,
        out Matrix<DoublePixel> rawSaliencyDistanceField,
        out Matrix<DoublePixel> saliencyDistanceField,
        out Matrix<Rgb8> meanShiftFiltered,
        out RegionGraph segmentationGraph,
        out Matrix<Rgb8> lodAbstracted,
        out WetInWetResult wetInWet,
        out Matrix<Rgb8> handTremor,
        out Matrix<DoublePixel>? edgeDensityField,
        out Matrix<DoublePixel>? turbulenceDensityField,
        out Matrix<Rgb8> densityAdjusted,
        out Matrix<Rgb8> toneAdjusted,
        out IReadOnlyMatrix<DoublePixel>? paperHeightMap,
        out Matrix<Rgb8> granulated,
        out IReadOnlyDictionary<WatercolorStep, WatercolorStepTiming> stepTimings)
    {
        image = Image;
        colorTransferred = ColorTransferred;
        selectedPalette = SelectedPalette;
        egbiPreBlurred = EgbiPreBlurred;
        egbiGraph = EgbiGraph;
        saliencyMap = SaliencyMap;
        salientMask = SalientMask;
        rawSaliencyDistanceField = RawSaliencyDistanceField;
        saliencyDistanceField = SaliencyDistanceField;
        meanShiftFiltered = MeanShiftFiltered;
        segmentationGraph = SegmentationGraph;
        lodAbstracted = LodAbstracted;
        wetInWet = WetInWet;
        handTremor = HandTremor;
        edgeDensityField = EdgeDensityField;
        turbulenceDensityField = TurbulenceDensityField;
        densityAdjusted = DensityAdjusted;
        toneAdjusted = ToneAdjusted;
        paperHeightMap = PaperHeightMap;
        granulated = Granulated;
        stepTimings = StepTimings;
    }

    /// <summary>
    /// Determines whether another <see cref="WatercolorResult"/> equals this instance by comparing all artifacts and step timings.
    /// </summary>
    /// <param name="other">The other <see cref="WatercolorResult"/> to compare.</param>
    /// <returns><see langword="true"/> if both instances have matching artifacts and step timings; otherwise <see langword="false"/>.</returns>
    public bool Equals(WatercolorResult? other) =>
        other is not null &&
        EqualityComparer<Matrix<Rgb8>>.Default.Equals(Image, other.Image) &&
        EqualityComparer<Matrix<Rgb8>>.Default.Equals(ColorTransferred, other.ColorTransferred) &&
        EqualityComparer<ColorTransferProfile>.Default.Equals(SelectedPalette, other.SelectedPalette) &&
        EqualityComparer<Matrix<Rgb8>>.Default.Equals(EgbiPreBlurred, other.EgbiPreBlurred) &&
        EqualityComparer<RegionGraph>.Default.Equals(EgbiGraph, other.EgbiGraph) &&
        EqualityComparer<Matrix<DoublePixel>>.Default.Equals(SaliencyMap, other.SaliencyMap) &&
        EqualityComparer<MaskMatrix>.Default.Equals(SalientMask, other.SalientMask) &&
        EqualityComparer<Matrix<DoublePixel>>.Default.Equals(RawSaliencyDistanceField, other.RawSaliencyDistanceField) &&
        EqualityComparer<Matrix<DoublePixel>>.Default.Equals(SaliencyDistanceField, other.SaliencyDistanceField) &&
        EqualityComparer<Matrix<Rgb8>>.Default.Equals(MeanShiftFiltered, other.MeanShiftFiltered) &&
        EqualityComparer<RegionGraph>.Default.Equals(SegmentationGraph, other.SegmentationGraph) &&
        EqualityComparer<Matrix<Rgb8>>.Default.Equals(LodAbstracted, other.LodAbstracted) &&
        EqualityComparer<WetInWetResult>.Default.Equals(WetInWet, other.WetInWet) &&
        EqualityComparer<Matrix<Rgb8>>.Default.Equals(HandTremor, other.HandTremor) &&
        EqualityComparer<Matrix<DoublePixel>?>.Default.Equals(EdgeDensityField, other.EdgeDensityField) &&
        EqualityComparer<Matrix<DoublePixel>?>.Default.Equals(TurbulenceDensityField, other.TurbulenceDensityField) &&
        EqualityComparer<Matrix<Rgb8>>.Default.Equals(DensityAdjusted, other.DensityAdjusted) &&
        EqualityComparer<Matrix<Rgb8>>.Default.Equals(ToneAdjusted, other.ToneAdjusted) &&
        EqualityComparer<IReadOnlyMatrix<DoublePixel>?>.Default.Equals(PaperHeightMap, other.PaperHeightMap) &&
        EqualityComparer<Matrix<Rgb8>>.Default.Equals(Granulated, other.Granulated) &&
        StepTimingsEqual(StepTimings, other.StepTimings);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Image);
        hash.Add(ColorTransferred);
        hash.Add(SelectedPalette);
        hash.Add(EgbiPreBlurred);
        hash.Add(EgbiGraph);
        hash.Add(SaliencyMap);
        hash.Add(SalientMask);
        hash.Add(RawSaliencyDistanceField);
        hash.Add(SaliencyDistanceField);
        hash.Add(MeanShiftFiltered);
        hash.Add(SegmentationGraph);
        hash.Add(LodAbstracted);
        hash.Add(WetInWet);
        hash.Add(HandTremor);
        hash.Add(EdgeDensityField);
        hash.Add(TurbulenceDensityField);
        hash.Add(DensityAdjusted);
        hash.Add(ToneAdjusted);
        hash.Add(PaperHeightMap);
        hash.Add(Granulated);

        int timingsHash = 0;
        foreach (var (step, timing) in StepTimings)
        {
            timingsHash ^= HashCode.Combine(step, timing);
        }

        hash.Add(timingsHash);
        return hash.ToHashCode();
    }

    private static bool StepTimingsEqual(
        IReadOnlyDictionary<WatercolorStep, WatercolorStepTiming> first,
        IReadOnlyDictionary<WatercolorStep, WatercolorStepTiming> second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        if (first.Count != second.Count)
        {
            return false;
        }

        foreach (var (step, timing) in first)
        {
            if (!second.TryGetValue(step, out WatercolorStepTiming otherTiming) || timing != otherTiming)
            {
                return false;
            }
        }

        return true;
    }
}

namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Orchestrates the complete Wang et al. (IEEE TVCG 2014) photo watercolorization pipeline across Stages 1–6.
/// </summary>
public static class WatercolorPipeline
{
    /// <summary>
    /// Executes the full watercolorization pipeline on <paramref name="image"/> and returns the final painting alongside intermediate stage outputs.
    /// </summary>
    /// <param name="image">The input RGB photograph to watercolorize.</param>
    /// <param name="options">Optional pipeline configuration parameters controlling each stage.</param>
    /// <param name="paperHeightMap">Optional custom normalized [0, 1] paper height map used during Stage 6 granulation.</param>
    /// <returns>A <see cref="WatercolorResult"/> containing the final rendered painting and stage intermediates.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="paperHeightMap"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> contains invalid parameters.</exception>
    public static WatercolorResult RenderDetailed(
        IReadOnlyMatrix<Rgb8> image,
        WatercolorOptions? options = null,
        IReadOnlyMatrix<DoublePixel>? paperHeightMap = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        WatercolorOptions opts = options ?? WatercolorOptions.Default;
        opts.Validate();
        Guard.ThrowIfSizeMismatchIfNotNull(image, paperHeightMap);

        if (image.Count == 0)
        {
            return CreateEmptyResult(image.Size, opts);
        }

        // Stage 1: Reinhard Color Transfer
        (Matrix<Rgb8> colorTransferred, ColorTransferProfile selectedPalette) = opts.TargetPalette is not null
            ? (ColorTransfer.Transfer(image, opts.TargetPalette, strength: opts.ColorTransferStrength), opts.TargetPalette)
            : ColorTransfer.TransferToClosestProfile(
                image,
                WatercolorPalettes.Defaults,
                featureWeights: WatercolorPalettes.DefaultFeatureWeights,
                strength: opts.ColorTransferStrength);

        // Stage 2: Regional Contrast Saliency & Saliency Distance Field
        Matrix<Rgb8> blurred = Blur.GaussianBlur(colorTransferred, opts.EgbiBlurSigma);
        Matrix<Lab32> labBlurred = blurred.Map(static pixel => pixel.ToLab32(RgbColorSpace.SRgb));
        EgbiOptions resolvedEgbi = opts.Egbi ??
            new EgbiOptions(
                KFactor: 50.0,
                Epsilon: 1.0,
                MinRegionSize: Math.Min(200, Math.Max(1, image.Count / 16)));
        IDisjointSet egbiSeg = EgbiSegmentation.Segment(labBlurred, resolvedEgbi);
        Matrix<DoublePixel> saliencyMap = RegionalContrastSaliency.ComputeSaliency(colorTransferred, egbiSeg, opts.RegionalContrastSaliency);
        (MaskMatrix salientMask, Matrix<DoublePixel> distanceField) = SaliencyDistanceField.Compute(
            colorTransferred,
            saliencyMap,
            opts.SaliencyCut,
            opts.SaliencyDistanceField);

        // Stage 3: Mean-Shift Segmentation & Saliency-Guided LOD Abstraction
        MeanShiftOptions resolvedMeanShift = opts.MeanShift ??
            new MeanShiftOptions(MinRegionSize: Math.Min(50, Math.Max(1, image.Count / 32)));
        (Matrix<Rgb8> meanShiftFiltered, IDisjointSet meanShiftSeg) = MeanShiftSegmentation.FilterAndSegment(colorTransferred, resolvedMeanShift);
        RegionGraph regionGraph = RegionGraph.FromDisjointSet(meanShiftSeg);
        Matrix<Rgb8> lodAbstracted = LodAbstraction.Apply(
            meanShiftFiltered,
            regionGraph,
            distanceField,
            salientMask,
            opts.LodAbstraction);

        // Stage 4: Wet-in-Wet Bleeding
        WetInWetResult wetInWet = WetInWetFilter.ApplyDetailed(
            lodAbstracted,
            regionGraph,
            salientMask,
            opts.WetInWet);

        // Stage 5: Hand Tremor Distortion
        Matrix<Rgb8> handTremor = HandTremorFilter.Apply(
            wetInWet.Image,
            regionGraph,
            distanceField,
            wetInWet,
            opts.HandTremor);

        // Stage 6: Secondary Watercolor Effects & FXAA
        EdgeDarkeningOptions edgeOpts = opts.EdgeDarkening ?? EdgeDarkeningOptions.Default;
        TurbulenceFlowOptions turbOpts = opts.TurbulenceFlow ?? TurbulenceFlowOptions.Default;

        Matrix<Rgb8> preTone;
        if (edgeOpts.Intensity == 0.0 && turbOpts.Intensity == 0.0)
        {
            preTone = handTremor;
        }
        else if (edgeOpts.Intensity == 0.0)
        {
            preTone = TurbulenceFlow.Apply(handTremor, turbOpts);
        }
        else if (turbOpts.Intensity == 0.0)
        {
            preTone = EdgeDarkening.Apply(handTremor, edgeOpts);
        }
        else
        {
            Matrix<DoublePixel> combinedDensity = EdgeDarkening.ComputeDensityField(handTremor, edgeOpts);
            Matrix<DoublePixel> turbulenceDensity = TurbulenceFlow.ComputeDensityField(handTremor.Size, turbOpts);
            for (int i = 0; i < combinedDensity.Count; i++)
            {
                combinedDensity[i] = new DoublePixel(combinedDensity[i].Value + turbulenceDensity[i].Value);
            }

            preTone = PigmentBlend.ApplyDensityField(handTremor, combinedDensity);
        }

        Matrix<Rgb8> toneAdjusted = PiecewiseToneCurve.Apply(preTone, opts.ToneCurve);
        Matrix<Rgb8> granulated = PaperGranulation.Apply(toneAdjusted, opts.PaperGranulation, paperHeightMap);
        Matrix<Rgb8> output = opts.EnableFxaa
            ? Fxaa.Apply(granulated, opts.Fxaa)
            : granulated;

        return new WatercolorResult(
            output,
            colorTransferred,
            selectedPalette,
            saliencyMap,
            salientMask,
            distanceField,
            regionGraph,
            lodAbstracted,
            wetInWet,
            handTremor);
    }

    /// <summary>
    /// Executes the full watercolorization pipeline on <paramref name="image"/> and returns the final rendered watercolor painting.
    /// </summary>
    /// <param name="image">The input RGB photograph to watercolorize.</param>
    /// <param name="options">Optional pipeline configuration parameters controlling each stage.</param>
    /// <param name="paperHeightMap">Optional custom normalized [0, 1] paper height map used during Stage 6 granulation.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the final rendered watercolor painting.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="paperHeightMap"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> contains invalid parameters.</exception>
    public static Matrix<Rgb8> Render(
        IReadOnlyMatrix<Rgb8> image,
        WatercolorOptions? options = null,
        IReadOnlyMatrix<DoublePixel>? paperHeightMap = null) =>
        RenderDetailed(image, options, paperHeightMap).Image;

    private static WatercolorResult CreateEmptyResult(Size size, WatercolorOptions options)
    {
        var emptyRgb = new SequentialMatrix<Rgb8>(size);
        var emptyDouble = new SequentialMatrix<DoublePixel>(size);
        var emptyMask = new MaskMatrix(size);
        var emptyGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(size));
        var emptyWetInWet = new WetInWetResult(emptyRgb, emptyMask, []);

        return new WatercolorResult(
            emptyRgb,
            emptyRgb,
            options.TargetPalette ?? WatercolorPalettes.Defaults[0],
            emptyDouble,
            emptyMask,
            emptyDouble,
            emptyGraph,
            emptyRgb,
            emptyWetInWet,
            emptyRgb);
    }
}

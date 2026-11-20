namespace Leiter.Algorithms.Internal;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Color;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Captures scale-invariant color dispersion and scene clutter statistics extracted from a canonical thumbnail.
/// </summary>
internal readonly record struct ImageTuningProfile(
    Size ImageSize,
    bool IsEmpty,
    LabColorStatistics LabStats,
    double LabDispersion,
    double ColorSpreadFactor,
    double ClutterFactor)
{
    /// <summary>
    /// The canonical thumbnail geometric-mean linear dimension used for scale-invariant image analysis.
    /// </summary>
    internal const double CanonicalDimension = 256.0;

    /// <summary>
    /// The minimum clamped color spread multiplier relative to <see cref="OptionTuning.ReferenceLabDispersion"/>.
    /// </summary>
    internal const double MinColorSpreadFactor = 0.75;

    /// <summary>
    /// The maximum clamped color spread multiplier relative to <see cref="OptionTuning.ReferenceLabDispersion"/>.
    /// </summary>
    internal const double MaxColorSpreadFactor = 1.35;

    /// <summary>
    /// The minimum clamped scene clutter multiplier relative to <see cref="OptionTuning.ReferenceClutterGradient"/>.
    /// </summary>
    internal const double MinClutterFactor = 0.75;

    /// <summary>
    /// The maximum clamped scene clutter multiplier relative to <see cref="OptionTuning.ReferenceClutterGradient"/>.
    /// </summary>
    internal const double MaxClutterFactor = 1.50;

    /// <summary>
    /// The geometric-mean image dimension below which clutter adaptation holds neutrally at <c>1.0</c>.
    /// </summary>
    internal const double ClutterRampStartDimension = 16.0;

    /// <summary>
    /// The linear ramp width above <see cref="ClutterRampStartDimension"/> over which clutter weight rises from <c>0</c> to <c>1</c> (reaching full weight at <c>64</c>).
    /// </summary>
    internal const double ClutterRampDimensionSpan = 48.0;

    /// <summary>
    /// Creates a neutral profile representing an empty image of dimensions <paramref name="size"/>.
    /// </summary>
    public static ImageTuningProfile CreateEmpty(Size size) =>
        new(size, IsEmpty: true, default, OptionTuning.ReferenceLabDispersion, 1.0, 1.0);

    /// <summary>
    /// Analyzes an <see cref="Rgb8"/> image using an anti-aliased canonical thumbnail to derive scale-invariant content metrics.
    /// </summary>
    public static ImageTuningProfile Analyze(IReadOnlyMatrix<Rgb8> image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Guard.ThrowIfNegative(image.Size);
        return AnalyzeUnchecked(image);
    }

    /// <summary>
    /// Analyzes a pre-validated <see cref="Rgb8"/> image using an anti-aliased canonical thumbnail.
    /// </summary>
    internal static ImageTuningProfile AnalyzeUnchecked(IReadOnlyMatrix<Rgb8> image)
    {
        if (image.Count == 0)
        {
            return CreateEmpty(image.Size);
        }

        double geomMean = OptionTuning.GeometricMeanDimension(image.Size);
        IReadOnlyMatrix<Rgb8> thumb = geomMean > CanonicalDimension
            ? image.ResizeBox(ComputeThumbnailSize(image.Size, geomMean))
            : image;

        LabColorStatistics labStats = ColorTransfer.ComputeStatistics(thumb, RgbColorSpace.SRgb, Illuminant.E);
        double clutterFactor = ComputeClutterFactor(thumb, image.Size);
        return FromStatistics(image.Size, labStats, clutterFactor);
    }

    /// <summary>
    /// Analyzes a <see cref="Lab32"/> image under <paramref name="illuminant"/> using an anti-aliased canonical thumbnail
    /// to derive scale-invariant content metrics.
    /// </summary>
    public static ImageTuningProfile Analyze(IReadOnlyMatrix<Lab32> labImage, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(labImage);
        Guard.ThrowIfNegative(labImage.Size);
        Guard.ThrowIfUndefinedEnum(illuminant);
        return AnalyzeUnchecked(labImage, illuminant);
    }

    /// <summary>
    /// Analyzes a pre-validated <see cref="Lab32"/> image under <paramref name="illuminant"/> using an anti-aliased canonical thumbnail.
    /// </summary>
    internal static ImageTuningProfile AnalyzeUnchecked(IReadOnlyMatrix<Lab32> labImage, Illuminant illuminant)
    {
        if (labImage.Count == 0)
        {
            return CreateEmpty(labImage.Size);
        }

        double geomMean = OptionTuning.GeometricMeanDimension(labImage.Size);
        IReadOnlyMatrix<Lab32> labThumb = geomMean > CanonicalDimension
            ? labImage.ResizeBox(ComputeThumbnailSize(labImage.Size, geomMean))
            : labImage;

        LabColorStatistics labStats = ColorTransfer.ComputeStatistics(labThumb);
        Matrix<Rgb8> rgbThumb = labThumb.ToRgb8(RgbColorSpace.SRgb, illuminant);
        double clutterFactor = ComputeClutterFactor(rgbThumb, labImage.Size);
        return FromStatistics(labImage.Size, labStats, clutterFactor);
    }

    /// <summary>
    /// Projects post-Stage-1 CIELAB statistics after Reinhard color transfer toward <paramref name="targetStats"/>.
    /// </summary>
    public ImageTuningProfile WithTransferredStatistics(LabColorStatistics targetStats, double strength)
    {
        if (IsEmpty)
        {
            return this;
        }

        double alpha = Math.Clamp(strength, 0.0, 1.0);
        LabColorStatistics effectiveStats = LabColorStatistics.Lerp(LabStats, targetStats, alpha);

        return FromStatistics(ImageSize, effectiveStats, ClutterFactor);
    }

    /// <summary>
    /// Computes the 3D CIELAB dispersion norm <c>sqrt(StdDevL² + StdDevA² + StdDevB²)</c>.
    /// </summary>
    public static double ComputeDispersion(LabColorStatistics stats) =>
        new Vector3D(stats.StdDevL, stats.StdDevA, stats.StdDevB).Length;

    private static ImageTuningProfile FromStatistics(Size imageSize, LabColorStatistics stats, double clutterFactor)
    {
        double dispersion = ComputeDispersion(stats);
        double colorSpread = Math.Clamp(dispersion / OptionTuning.ReferenceLabDispersion, MinColorSpreadFactor, MaxColorSpreadFactor);
        return new ImageTuningProfile(imageSize, IsEmpty: false, stats, dispersion, colorSpread, clutterFactor);
    }

    private static double ComputeClutterFactor(IReadOnlyMatrix<Rgb8> thumb, Size originalSize)
    {
        var (_, _, magnitude) = SpatialGradients.ComputeColorGradients(thumb);
        double sum = 0.0;
        int count = magnitude.Count;
        for (int i = 0; i < count; i++)
        {
            sum += magnitude[i].Value;
        }

        double meanEdge = sum / count;
        double thumbGeomMean = OptionTuning.GeometricMeanDimension(thumb.Size);
        double normalizedMeanEdge = meanEdge * (thumbGeomMean / CanonicalDimension);
        double rawClutter = Math.Clamp(normalizedMeanEdge / OptionTuning.ReferenceClutterGradient, MinClutterFactor, MaxClutterFactor);

        double origGeomMean = OptionTuning.GeometricMeanDimension(originalSize);
        double weight = Math.Clamp((origGeomMean - ClutterRampStartDimension) / ClutterRampDimensionSpan, 0.0, 1.0);
        return ScalarMath.Lerp(1.0, rawClutter, weight);
    }

    private static Size ComputeThumbnailSize(Size sourceSize, double sourceGeomMean)
    {
        double scale = CanonicalDimension / sourceGeomMean;
        int thumbW = Math.Clamp((int)Math.Round(sourceSize.Width * scale, MidpointRounding.AwayFromZero), 1, sourceSize.Width);
        int thumbH = Math.Clamp((int)Math.Round(sourceSize.Height * scale, MidpointRounding.AwayFromZero), 1, sourceSize.Height);
        return new Size(thumbW, thumbH);
    }
}

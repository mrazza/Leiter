namespace Leiter.Algorithms.Internal;

using System;
using System.Threading.Tasks;
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
    private const double CanonicalDimension = 256.0;

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

        if (image.Count == 0)
        {
            return CreateEmpty(image.Size);
        }

        double geomMean = Math.Sqrt((double)image.Width * image.Height);
        IReadOnlyMatrix<Rgb8> thumb = geomMean > CanonicalDimension
            ? DownsampleBox<Rgb8, Rgb8BoxAccumulator>(image, geomMean)
            : image;

        LabColorStatistics labStats = ColorTransfer.ComputeStatistics(thumb, RgbColorSpace.sRGB, Illuminant.E);
        double clutterFactor = ComputeClutterFactor(thumb, image.Size);
        return FromStatistics(image.Size, labStats, clutterFactor);
    }

    /// <summary>
    /// Analyzes a <see cref="Lab32"/> image using an anti-aliased canonical thumbnail to derive scale-invariant content metrics.
    /// </summary>
    public static ImageTuningProfile Analyze(IReadOnlyMatrix<Lab32> labImage)
    {
        ArgumentNullException.ThrowIfNull(labImage);

        if (labImage.Count == 0)
        {
            return CreateEmpty(labImage.Size);
        }

        double geomMean = Math.Sqrt((double)labImage.Width * labImage.Height);
        IReadOnlyMatrix<Lab32> labThumb = geomMean > CanonicalDimension
            ? DownsampleBox<Lab32, Lab32BoxAccumulator>(labImage, geomMean)
            : labImage;

        LabColorStatistics labStats = ColorTransfer.ComputeStatistics(labThumb);
        Matrix<Rgb8> rgbThumb = labThumb.Map(static p => p.ToRgb8(RgbColorSpace.sRGB));
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
        var effectiveStats = new LabColorStatistics(
            LabStats.MeanL + (alpha * (targetStats.MeanL - LabStats.MeanL)),
            LabStats.MeanA + (alpha * (targetStats.MeanA - LabStats.MeanA)),
            LabStats.MeanB + (alpha * (targetStats.MeanB - LabStats.MeanB)),
            LabStats.StdDevL + (alpha * (targetStats.StdDevL - LabStats.StdDevL)),
            LabStats.StdDevA + (alpha * (targetStats.StdDevA - LabStats.StdDevA)),
            LabStats.StdDevB + (alpha * (targetStats.StdDevB - LabStats.StdDevB)));

        return FromStatistics(ImageSize, effectiveStats, ClutterFactor);
    }

    /// <summary>
    /// Computes the 3D CIELAB dispersion norm <c>sqrt(StdDevL² + StdDevA² + StdDevB²)</c>.
    /// </summary>
    public static double ComputeDispersion(LabColorStatistics stats) =>
        Math.Sqrt((stats.StdDevL * stats.StdDevL) + (stats.StdDevA * stats.StdDevA) + (stats.StdDevB * stats.StdDevB));

    private static ImageTuningProfile FromStatistics(Size imageSize, LabColorStatistics stats, double clutterFactor)
    {
        double dispersion = ComputeDispersion(stats);
        double colorSpread = Math.Clamp(dispersion / OptionTuning.ReferenceLabDispersion, 0.75, 1.35);
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
        double thumbGeomMean = Math.Sqrt((double)thumb.Width * thumb.Height);
        double normalizedMeanEdge = meanEdge * (thumbGeomMean / CanonicalDimension);
        double rawClutter = Math.Clamp(normalizedMeanEdge / OptionTuning.ReferenceClutterGradient, 0.75, 1.50);

        double origGeomMean = Math.Sqrt((double)originalSize.Width * originalSize.Height);
        double weight = Math.Clamp((origGeomMean - 16.0) / 48.0, 0.0, 1.0);
        return 1.0 + (weight * (rawClutter - 1.0));
    }

    private static Matrix<TPixel> DownsampleBox<TPixel, TAccumulator>(IReadOnlyMatrix<TPixel> source, double sourceGeomMean)
        where TPixel : struct
        where TAccumulator : struct, IBoxAccumulator<TPixel>
    {
        double scale = CanonicalDimension / sourceGeomMean;
        int thumbW = Math.Clamp((int)Math.Round(source.Width * scale, MidpointRounding.AwayFromZero), 1, source.Width);
        int thumbH = Math.Clamp((int)Math.Round(source.Height * scale, MidpointRounding.AwayFromZero), 1, source.Height);

        var thumb = new SequentialMatrix<TPixel>(thumbW, thumbH);
        int srcW = source.Width;
        int srcH = source.Height;

        Parallel.For(0, thumbH, ty =>
        {
            int y0 = Math.Min((int)((long)ty * srcH / thumbH), srcH - 1);
            int y1 = Math.Clamp((int)((long)(ty + 1) * srcH / thumbH), y0 + 1, srcH);

            for (int tx = 0; tx < thumbW; tx++)
            {
                int x0 = Math.Min((int)((long)tx * srcW / thumbW), srcW - 1);
                int x1 = Math.Clamp((int)((long)(tx + 1) * srcW / thumbW), x0 + 1, srcW);

                TAccumulator accumulator = default;
                for (int sy = y0; sy < y1; sy++)
                {
                    for (int sx = x0; sx < x1; sx++)
                    {
                        accumulator.Add(source[sx, sy]);
                    }
                }

                double invCount = 1.0 / ((x1 - x0) * (y1 - y0));
                thumb[tx, ty] = accumulator.ToAverage(invCount);
            }
        });

        return thumb;
    }

    private interface IBoxAccumulator<TPixel>
        where TPixel : struct
    {
        void Add(TPixel pixel);
        TPixel ToAverage(double invCount);
    }

    private struct Rgb8BoxAccumulator : IBoxAccumulator<Rgb8>
    {
        private long sumR;
        private long sumG;
        private long sumB;

        public void Add(Rgb8 pixel)
        {
            sumR += pixel.R;
            sumG += pixel.G;
            sumB += pixel.B;
        }

        public readonly Rgb8 ToAverage(double invCount) =>
            new(
                (byte)Math.Round(sumR * invCount, MidpointRounding.AwayFromZero),
                (byte)Math.Round(sumG * invCount, MidpointRounding.AwayFromZero),
                (byte)Math.Round(sumB * invCount, MidpointRounding.AwayFromZero));
    }

    private struct Lab32BoxAccumulator : IBoxAccumulator<Lab32>
    {
        private double sumL;
        private double sumA;
        private double sumB;

        public void Add(Lab32 pixel)
        {
            sumL += pixel.L;
            sumA += pixel.A;
            sumB += pixel.B;
        }

        public readonly Lab32 ToAverage(double invCount) =>
            new((float)(sumL * invCount), (float)(sumA * invCount), (float)(sumB * invCount));
    }
}

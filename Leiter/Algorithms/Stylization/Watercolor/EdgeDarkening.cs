namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Applies edge darkening to simulate watercolor pigment accumulation at drying boundaries (Bousseau et al. 2006).
/// </summary>
public static class EdgeDarkening
{
    /// <summary>
    /// Computes spatial color gradients, optionally blurs the edge response, and applies density adjustments to darken edges.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="options">Optional edge darkening configuration parameters. If <see langword="null"/>, default options are used.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the edge-darkened image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> are invalid.</exception>
    public static Matrix<Rgb8> Apply(IReadOnlyMatrix<Rgb8> image, EdgeDarkeningOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        EdgeDarkeningOptions opts = options ?? EdgeDarkeningOptions.Default;
        opts.Validate();

        if (image.Count == 0 || opts.Intensity == 0.0)
        {
            return image.Clone();
        }

        Matrix<DoublePixel> densityField = ComputeDensityField(image, opts);
        return PigmentBlend.ApplyDensityField(image, densityField);
    }

    internal static Matrix<DoublePixel> ComputeDensityField(IReadOnlyMatrix<Rgb8> image, EdgeDarkeningOptions opts)
    {
        var (_, _, edgeStrength) = SpatialGradients.ComputeColorGradients(image, opts.GradientOperator);

        Matrix<DoublePixel> smoothedEdges = opts.BlurSigma > 0.0
            ? Blur.GaussianBlurRecursive(edgeStrength, opts.BlurSigma)
            : edgeStrength;

        double intensity = opts.Intensity;
        for (int i = 0; i < smoothedEdges.Count; i++)
        {
            smoothedEdges[i] = new DoublePixel(intensity * Math.Clamp(smoothedEdges[i].Value, 0.0, 1.0));
        }

        return smoothedEdges;
    }
}

namespace Leiter.Algorithms.AntiAliasing;

using System;
using System.Threading.Tasks;
using Leiter.Algorithms;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Implements Timothy Lottes' Fast Approximate Anti-Aliasing (FXAA 3.11).
/// </summary>
public static class Fxaa
{
    /// <summary>
    /// Applies FXAA 3.11 anti-aliasing to an RGB image.
    /// </summary>
    /// <param name="image">The input RGB image to anti-alias.</param>
    /// <param name="options">Optional FXAA configuration parameters.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the anti-aliased image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static Matrix<Rgb8> Apply(IReadOnlyMatrix<Rgb8> image, FxaaOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        FxaaOptions opts = options ?? FxaaOptions.Default;
        opts.Validate();

        if (image.Count == 0)
            return new SequentialMatrix<Rgb8>(image.Size);

        int width = image.Width;
        int height = image.Height;

        double[] luma = image.MapToArray(static pixel => pixel.GetLuma());

        var result = new SequentialMatrix<Rgb8>(image.Size);

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                double lumaM = GetLumaClamped(luma, x, y, width, height);
                double lumaN = GetLumaClamped(luma, x, y - 1, width, height);
                double lumaS = GetLumaClamped(luma, x, y + 1, width, height);
                double lumaW = GetLumaClamped(luma, x - 1, y, width, height);
                double lumaE = GetLumaClamped(luma, x + 1, y, width, height);

                double maxLuma = Math.Max(lumaM, Math.Max(Math.Max(lumaN, lumaS), Math.Max(lumaW, lumaE)));
                double minLuma = Math.Min(lumaM, Math.Min(Math.Min(lumaN, lumaS), Math.Min(lumaW, lumaE)));
                double range = maxLuma - minLuma;

                double contrastThreshold = Math.Max(opts.AbsoluteContrastThreshold, maxLuma * opts.RelativeContrastThreshold);
                if (range < contrastThreshold)
                {
                    result[x, y] = image[x, y];
                    continue;
                }

                double lumaNW = GetLumaClamped(luma, x - 1, y - 1, width, height);
                double lumaNE = GetLumaClamped(luma, x + 1, y - 1, width, height);
                double lumaSW = GetLumaClamped(luma, x - 1, y + 1, width, height);
                double lumaSE = GetLumaClamped(luma, x + 1, y + 1, width, height);

                // Edge orientation using FXAA 3.11 second-derivative contrast across 3x3 neighborhood
                double edgeHorz =
                    Math.Abs((-2.0 * lumaW) + lumaNW + lumaSW)
                    + (2.0 * Math.Abs((-2.0 * lumaM) + lumaN + lumaS))
                    + Math.Abs((-2.0 * lumaE) + lumaNE + lumaSE);
                double edgeVert =
                    Math.Abs((-2.0 * lumaN) + lumaNW + lumaNE)
                    + (2.0 * Math.Abs((-2.0 * lumaM) + lumaW + lumaE))
                    + Math.Abs((-2.0 * lumaS) + lumaSW + lumaSE);
                bool isHorizontal = edgeHorz >= edgeVert;

                // Step direction perpendicular to the edge
                double lumaPos = isHorizontal ? lumaS : lumaE;
                double lumaNeg = isHorizontal ? lumaN : lumaW;
                double gradientPos = Math.Abs(lumaPos - lumaM);
                double gradientNeg = Math.Abs(lumaNeg - lumaM);

                double stepSign;
                double lumaOpposite;
                double maxGrad;

                if (gradientPos >= gradientNeg)
                {
                    stepSign = 1.0;
                    lumaOpposite = lumaPos;
                    maxGrad = gradientPos;
                }
                else
                {
                    stepSign = -1.0;
                    lumaOpposite = lumaNeg;
                    maxGrad = gradientNeg;
                }

                double lumaLocalAvg = 0.5 * (lumaM + lumaOpposite);
                double gradientThreshold = 0.25 * maxGrad;

                // Subpixel blend factor
                double lumaSubpixelAvg = ((2.0 * (lumaN + lumaS + lumaW + lumaE)) + lumaNW + lumaNE + lumaSW + lumaSE) / 12.0;
                double subpixelFilter = Math.Clamp(Math.Abs(lumaSubpixelAvg - lumaM) / range, 0.0, 1.0);
                double s = ScalarMath.Smoothstep(subpixelFilter);
                double subpixelOffset = s * s * opts.SubpixelBlendStrength;

                // Bilateral span search along the edge tangent
                double edgeBlend = 0.0;
                if (maxGrad > 0.0)
                {
                    int stepOffset = (int)stepSign;
                    var (distPos, lumaEndPos, foundPos) = SearchSpanEndpoint(
                        luma, x, y, width, height, isHorizontal, 1, stepOffset,
                        lumaLocalAvg, gradientThreshold, opts.MaxSpanSearchSteps);
                    var (distNeg, lumaEndNeg, foundNeg) = SearchSpanEndpoint(
                        luma, x, y, width, height, isHorizontal, -1, stepOffset,
                        lumaLocalAvg, gradientThreshold, opts.MaxSpanSearchSteps);

                    double spanLength = distPos + distNeg;
                    bool isPosCloser = distPos < distNeg;
                    double nearDist = isPosCloser ? distPos : distNeg;
                    double lumaEndNear = isPosCloser ? lumaEndPos : lumaEndNeg;
                    bool nearFound = isPosCloser ? foundPos : foundNeg;

                    // Check if closer endpoint was reached and has opposite sign relative to lumaLocalAvg - lumaM
                    bool goodSpan = nearFound && ((lumaM < lumaLocalAvg) != (lumaEndNear < lumaLocalAvg));
                    if (goodSpan)
                    {
                        edgeBlend = Math.Max(0.0, 0.5 - (nearDist / spanLength));
                    }
                }

                double finalOffset = Math.Max(subpixelOffset, edgeBlend);

                if (finalOffset <= 0.0)
                {
                    result[x, y] = image[x, y];
                }
                else
                {
                    double sampleX = x + (isHorizontal ? 0.0 : stepSign * finalOffset);
                    double sampleY = y + (isHorizontal ? stepSign * finalOffset : 0.0);
                    result[x, y] = image.SampleBilinear(sampleX, sampleY, EdgeHandling.Extend);
                }
            }
        });

        return result;
    }

    private static (double Dist, double EndLuma, bool Found) SearchSpanEndpoint(
        double[] luma,
        int x,
        int y,
        int width,
        int height,
        bool isHorizontal,
        int tangentSign,
        int stepOffset,
        double lumaLocalAvg,
        double gradientThreshold,
        int maxSteps)
    {
        for (int step = 1; step <= maxSteps; step++)
        {
            int signedStep = step * tangentSign;
            int tx = isHorizontal ? x + signedStep : x;
            int ty = isHorizontal ? y : y + signedStep;
            int ox = isHorizontal ? tx : tx + stepOffset;
            int oy = isHorizontal ? ty + stepOffset : ty;

            double sampleLuma = 0.5 * (GetLumaClamped(luma, tx, ty, width, height) + GetLumaClamped(luma, ox, oy, width, height));
            if (Math.Abs(sampleLuma - lumaLocalAvg) >= gradientThreshold)
                return (step, sampleLuma, true);
        }

        return (maxSteps, lumaLocalAvg, false);
    }

    private static double GetLumaClamped(double[] luma, int x, int y, int width, int height)
    {
        int cx = Math.Clamp(x, 0, width - 1);
        int cy = Math.Clamp(y, 0, height - 1);
        return luma[(cy * width) + cx];
    }
}

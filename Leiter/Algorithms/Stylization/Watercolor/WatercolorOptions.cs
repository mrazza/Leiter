namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;

/// <summary>
/// Configures end-to-end parameters for Wang et al.'s artistic photo watercolorization pipeline (Stages 1–6).
/// </summary>
/// <param name="TargetPalette">Optional explicit watercolor target palette; when <see langword="null"/>, the closest palette is selected automatically.</param>
/// <param name="ColorTransferStrength">The Reinhard color transfer blending strength in [0.0, 1.0] (default 1.0).</param>
/// <param name="SaliencyCut">Optional configuration for iterative salient region extraction in Stage 2.</param>
/// <param name="MeanShift">Optional configuration for CIELAB Mean-Shift segmentation in Stage 3 (when <see langword="null"/>, uses Wang et al. defaults scaled safely for tiny images).</param>
/// <param name="LodAbstraction">Optional configuration for saliency-guided level-of-detail abstraction in Stage 3.</param>
/// <param name="WetInWet">Optional configuration for wet-in-wet edge bleeding in Stage 4.</param>
/// <param name="HandTremor">Optional configuration for hand-tremor boundary perturbation in Stage 5.</param>
/// <param name="EdgeDarkening">Optional configuration for pigment edge darkening in Stage 6.</param>
/// <param name="TurbulenceFlow">Optional configuration for low-frequency turbulence wash variation in Stage 6.</param>
/// <param name="ToneCurve">Optional configuration for the piecewise tone reproduction curve applied prior to paper granulation in Stage 6.</param>
/// <param name="PaperGranulation">Optional configuration for paper fiber granulation and micro-warp in Stage 6.</param>
/// <param name="Fxaa">Optional configuration for final full-screen FXAA anti-aliasing in Stage 6.</param>
/// <param name="EnableFxaa">Whether to apply final FXAA boundary smoothing (default <see langword="true"/>).</param>
public sealed record WatercolorOptions(
    WatercolorPalette? TargetPalette = null,
    double ColorTransferStrength = 1.0,
    SaliencyCutOptions? SaliencyCut = null,
    MeanShiftOptions? MeanShift = null,
    LodAbstractionOptions? LodAbstraction = null,
    WetInWetOptions? WetInWet = null,
    HandTremorOptions? HandTremor = null,
    EdgeDarkeningOptions? EdgeDarkening = null,
    TurbulenceFlowOptions? TurbulenceFlow = null,
    PiecewiseToneCurveOptions? ToneCurve = null,
    PaperGranulationOptions? PaperGranulation = null,
    FxaaOptions? Fxaa = null,
    bool EnableFxaa = true)
{
    /// <summary>
    /// Validates that all configured pipeline options are finite and within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any numeric or enum option is invalid.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(ColorTransferStrength) || ColorTransferStrength < 0.0 || ColorTransferStrength > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(ColorTransferStrength), ColorTransferStrength, "ColorTransferStrength must be finite and within [0, 1].");
        }

        SaliencyCut?.Validate();
        MeanShift?.Validate();
        LodAbstraction?.Validate();
        WetInWet?.Validate();
        HandTremor?.Validate();
        EdgeDarkening?.Validate();
        TurbulenceFlow?.Validate();
        ToneCurve?.Validate();
        PaperGranulation?.Validate();
        Fxaa?.Validate();
    }
}

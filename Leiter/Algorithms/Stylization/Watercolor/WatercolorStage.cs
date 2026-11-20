namespace Leiter.Algorithms.Stylization.Watercolor;

/// <summary>
/// Identifies the major execution stages of <see cref="WatercolorPipeline"/>.
/// </summary>
public enum WatercolorStage
{
    /// <summary>
    /// Stage 1: Reinhard color transfer to a watercolor palette.
    /// </summary>
    ColorTransfer,

    /// <summary>
    /// Stage 2: EGBI pre-segmentation, regional contrast saliency, SaliencyCut masking, and distance field generation.
    /// </summary>
    Saliency,

    /// <summary>
    /// Stage 3a: Mean-Shift filtering, segmentation, and region adjacency graph construction.
    /// </summary>
    Segmentation,

    /// <summary>
    /// Stage 3b: Saliency-guided level-of-detail region abstraction.
    /// </summary>
    LodAbstraction,

    /// <summary>
    /// Stage 4: Wet-in-wet pigment scattering and oriented elliptical smoothing.
    /// </summary>
    WetInWet,

    /// <summary>
    /// Stage 5: Hand tremor boundary displacement, paper gaps, and pigment overlap blending.
    /// </summary>
    HandTremor,

    /// <summary>
    /// Stage 6: Edge darkening, turbulence flow, tone curve adjustment, paper granulation, and optional FXAA.
    /// </summary>
    Finishing
}

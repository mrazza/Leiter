namespace Leiter.Algorithms.Stylization.Watercolor;

/// <summary>
/// Identifies the fine-grained execution steps in the <see cref="WatercolorPipeline"/> dependency DAG.
/// </summary>
public enum WatercolorStep
{
    /// <summary>
    /// Stage 1: Reinhard color transfer to the selected watercolor palette.
    /// </summary>
    ColorTransfer,

    /// <summary>
    /// Stage 2a: Gaussian pre-blur and CIELAB conversion prior to EGBI over-segmentation.
    /// </summary>
    EgbiPreBlur,

    /// <summary>
    /// Stage 2b: Efficient Graph-Based Image (EGBI) over-segmentation and region graph construction.
    /// </summary>
    EgbiSegmentation,

    /// <summary>
    /// Stage 2c: Regional contrast saliency map computation.
    /// </summary>
    RegionalContrastSaliency,

    /// <summary>
    /// Stage 2d: Iterative SaliencyCut foreground mask extraction.
    /// </summary>
    SaliencyCut,

    /// <summary>
    /// Stage 2e–2f: Normalized Jump-Flooding saliency distance field synthesis and Gaussian smoothing.
    /// </summary>
    SaliencyDistanceField,

    /// <summary>
    /// Stage 3a-i: Edge-preserving CIELAB Mean-Shift mode seeking and filtering.
    /// </summary>
    MeanShiftFiltering,

    /// <summary>
    /// Stage 3a-ii: Mean-Shift region clustering, adjacency graph construction, and salient overlap fraction computation.
    /// </summary>
    MeanShiftClustering,

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
    /// Stage 6a-i: Pigment edge darkening density field computation.
    /// </summary>
    EdgeDarkening,

    /// <summary>
    /// Stage 6a-ii: Low-frequency turbulence flow density field computation.
    /// </summary>
    TurbulenceFlow,

    /// <summary>
    /// Stage 6a-iii: Combined edge-darkening and turbulence pigment wash density application.
    /// </summary>
    PigmentCombine,

    /// <summary>
    /// Stage 6b: Piecewise tone reproduction curve adjustment.
    /// </summary>
    ToneCurve,

    /// <summary>
    /// Stage 6c-i: Procedural watercolor paper surface height map synthesis.
    /// </summary>
    PaperHeightMap,

    /// <summary>
    /// Stage 6c-ii: Paper fiber granulation, micro-warp displacement, and valley pigment settling.
    /// </summary>
    PaperGranulation,

    /// <summary>
    /// Stage 6d: Final full-screen FXAA anti-aliasing pass.
    /// </summary>
    Fxaa
}

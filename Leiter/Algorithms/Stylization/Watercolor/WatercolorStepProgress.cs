namespace Leiter.Algorithms.Stylization.Watercolor;

using System;

/// <summary>
/// Reports completion of a fine-grained <see cref="WatercolorPipeline"/> execution step.
/// </summary>
/// <param name="Step">The pipeline step that completed.</param>
/// <param name="Stage">The major pipeline stage to which <paramref name="Step"/> belongs.</param>
/// <param name="Elapsed">The elapsed wall-clock duration of <paramref name="Step"/>.</param>
/// <param name="FromCache"><see langword="true"/> if the step output was reused from a previous cached execution; otherwise <see langword="false"/>.</param>
/// <param name="Bypassed"><see langword="true"/> if the step was bypassed due to an option toggle or no-op configuration; otherwise <see langword="false"/>.</param>
public readonly record struct WatercolorStepProgress(
    WatercolorStep Step,
    WatercolorStage Stage,
    TimeSpan Elapsed,
    bool FromCache,
    bool Bypassed);

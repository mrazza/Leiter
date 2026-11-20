namespace Leiter.Algorithms.Stylization.Watercolor;

using System;

/// <summary>
/// Reports completion of a <see cref="WatercolorPipeline"/> stage.
/// </summary>
/// <param name="Stage">The pipeline stage that completed.</param>
/// <param name="Elapsed">The elapsed wall-clock duration of <paramref name="Stage"/>.</param>
public readonly record struct WatercolorStageProgress(WatercolorStage Stage, TimeSpan Elapsed);

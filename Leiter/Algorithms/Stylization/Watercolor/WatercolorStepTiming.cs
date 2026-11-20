namespace Leiter.Algorithms.Stylization.Watercolor;

using System;

/// <summary>
/// Records execution timing and cache/bypass metadata for a single <see cref="WatercolorStep"/>.
/// </summary>
/// <param name="Elapsed">The elapsed wall-clock duration of the step.</param>
/// <param name="FromCache"><see langword="true"/> if the step output was reused from a previous cached execution; otherwise <see langword="false"/>.</param>
/// <param name="Bypassed"><see langword="true"/> if the step was bypassed due to an option toggle or no-op configuration; otherwise <see langword="false"/>.</param>
public readonly record struct WatercolorStepTiming(
    TimeSpan Elapsed,
    bool FromCache,
    bool Bypassed);

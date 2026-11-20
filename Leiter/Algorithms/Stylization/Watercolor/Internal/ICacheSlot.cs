namespace Leiter.Algorithms.Stylization.Watercolor.Internal;

/// <summary>
/// Provides type-erased cache clearing for <see cref="WatercolorPipelineSession"/> slots.
/// </summary>
internal interface ICacheSlot
{
    void Clear();
}

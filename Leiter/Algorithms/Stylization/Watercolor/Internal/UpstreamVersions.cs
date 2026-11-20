namespace Leiter.Algorithms.Stylization.Watercolor.Internal;

/// <summary>
/// Captures the monotonic version tokens of up to four upstream cache dependencies.
/// </summary>
internal readonly record struct UpstreamVersions(long A = 0, long B = 0, long C = 0, long D = 0);

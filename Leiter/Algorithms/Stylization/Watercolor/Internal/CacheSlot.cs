namespace Leiter.Algorithms.Stylization.Watercolor.Internal;

using System.Collections.Generic;

/// <summary>
/// Stores a cached pipeline step output keyed by upstream dependency versions and resolved step options.
/// </summary>
internal sealed class CacheSlot<TOptions, TValue> : ICacheSlot
{
    public bool HasValue { get; private set; }

    public UpstreamVersions Upstream { get; private set; }

    public TOptions? Options { get; private set; }

    public TValue Value { get; private set; } = default!;

    public long Version { get; private set; }

    public bool Matches(in UpstreamVersions upstream, TOptions? options) =>
        HasValue &&
        Upstream == upstream &&
        EqualityComparer<TOptions?>.Default.Equals(Options, options);

    public void Set(in UpstreamVersions upstream, TOptions? options, TValue value, long version)
    {
        Upstream = upstream;
        Options = options;
        Value = value;
        Version = version;
        HasValue = true;
    }

    public void Clear()
    {
        HasValue = false;
        Upstream = default;
        Options = default;
        Value = default!;
    }
}

namespace Leiter.Algorithms.Quantization;

using System.Collections.Generic;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;

/// <summary>
/// Represents the result of coverage-truncated palette quantization on an image.
/// </summary>
/// <typeparam name="TPixel">The quantized pixel type of the output palette and image.</typeparam>
/// <typeparam name="TMetric">The metric pixel type used to measure distances between palette entries.</typeparam>
/// <param name="Image">The quantized image where every pixel maps to a retained palette entry.</param>
/// <param name="Histogram">The frequency histogram over the retained palette entries after remapping rare buckets.</param>
/// <param name="MetricLookup">Lookup table mapping each retained palette entry to its precomputed metric value.</param>
public readonly record struct QuantizedPalette<TPixel, TMetric>(
    Matrix<TPixel> Image,
    IReadOnlyHistogram<TPixel> Histogram,
    IReadOnlyDictionary<TPixel, TMetric> MetricLookup)
    where TPixel : struct, IPixel<TPixel>
    where TMetric : struct, IPixel<TMetric>;

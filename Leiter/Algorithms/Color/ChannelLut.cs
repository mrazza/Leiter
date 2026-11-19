namespace Leiter.Algorithms.Color;

using System;
using System.Threading.Tasks;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Builds and applies 256-entry per-channel lookup tables (LUTs) to 8-bit RGB images.
/// </summary>
public static class ChannelLut
{
    /// <summary>
    /// Precomputes a 256-entry byte lookup table by sampling a normalized curve over <c>[0.0, 1.0]</c>.
    /// </summary>
    /// <param name="normalizedMapping">Maps each normalized input channel <c>i / 255.0</c> to a normalized output value in <c>[0.0, 1.0]</c>.</param>
    /// <returns>A 256-element byte lookup table indexed by source channel byte value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="normalizedMapping"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="normalizedMapping"/> produces a non-finite value.</exception>
    public static byte[] Build(Func<double, double> normalizedMapping)
    {
        ArgumentNullException.ThrowIfNull(normalizedMapping);

        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            double mapped = normalizedMapping(i / 255.0);
            Guard.ThrowIfNotFinite(mapped, nameof(normalizedMapping));
            lut[i] = Rgb8.ClampRoundedByte(mapped * 255.0);
        }

        return lut;
    }

    /// <summary>
    /// Applies a shared 256-entry lookup table uniformly to the red, green, and blue channels of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The source 8-bit RGB image.</param>
    /// <param name="lut">The 256-entry channel lookup table.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the LUT-mapped pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="lut"/> does not have length <c>256</c>.</exception>
    public static Matrix<Rgb8> Apply(IReadOnlyMatrix<Rgb8> image, ReadOnlySpan<byte> lut)
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateLutLength(lut, nameof(lut));

        if (image.Count == 0)
            return new SequentialMatrix<Rgb8>(image.Size);

        byte[] table = lut.ToArray();
        return ApplyCore(image, table, table, table);
    }

    /// <summary>
    /// Applies separate 256-entry lookup tables to the red, green, and blue channels of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The source 8-bit RGB image.</param>
    /// <param name="redLut">The 256-entry lookup table for the red channel.</param>
    /// <param name="greenLut">The 256-entry lookup table for the green channel.</param>
    /// <param name="blueLut">The 256-entry lookup table for the blue channel.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the LUT-mapped pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when any lookup table does not have length <c>256</c>.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        ReadOnlySpan<byte> redLut,
        ReadOnlySpan<byte> greenLut,
        ReadOnlySpan<byte> blueLut)
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateLutLength(redLut, nameof(redLut));
        ValidateLutLength(greenLut, nameof(greenLut));
        ValidateLutLength(blueLut, nameof(blueLut));

        if (image.Count == 0)
            return new SequentialMatrix<Rgb8>(image.Size);

        return ApplyCore(image, redLut.ToArray(), greenLut.ToArray(), blueLut.ToArray());
    }

    private static Matrix<Rgb8> ApplyCore(
        IReadOnlyMatrix<Rgb8> image,
        byte[] redTable,
        byte[] greenTable,
        byte[] blueTable)
    {
        int width = image.Width;
        int height = image.Height;
        var result = new SequentialMatrix<Rgb8>(width, height);

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                Rgb8 pixel = image[x, y];
                result[x, y] = new Rgb8(redTable[pixel.R], greenTable[pixel.G], blueTable[pixel.B]);
            }
        });

        return result;
    }

    private static void ValidateLutLength(ReadOnlySpan<byte> lut, string paramName)
    {
        if (lut.Length != 256)
            throw new ArgumentException($"Lookup table length must be 256, but was {lut.Length}.", paramName);
    }
}

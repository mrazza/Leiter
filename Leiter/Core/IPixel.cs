namespace Leiter.Core;

/// <summary>
/// Represents the value of an element in a matrix made up of one or more primitive fields; usually a pixel in an image.
/// </summary>
/// <typeparam name="P">The type of the pixel.</typeparam>
public interface IPixel<P> : ISelfOperable<P>, INumericOperable<P>, IScalarOperable<P>
    where P : struct, IPixel<P>
{
    /// <summary>
    /// Computes the distance between this pixel and another pixel.
    /// </summary>
    /// <param name="otherPixel">The other pixel to measure the distance to.</param>
    /// <returns>The computed distance between the two pixels.</returns>
    double Distance(P otherPixel);
}

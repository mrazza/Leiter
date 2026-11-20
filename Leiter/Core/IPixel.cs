namespace Leiter.Core;

using System.Runtime.Intrinsics;

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

    /// <summary>
    /// Linearly interpolates component-wise between <paramref name="from"/> and <paramref name="to"/> by <paramref name="amount"/>.
    /// </summary>
    /// <param name="from">The starting pixel value returned when <paramref name="amount"/> is <c>0.0</c>.</param>
    /// <param name="to">The target pixel value returned when <paramref name="amount"/> is <c>1.0</c>.</param>
    /// <param name="amount">The interpolation factor, typically in <c>[0.0, 1.0]</c>.</param>
    /// <returns>The component-wise linearly interpolated pixel value.</returns>
    static virtual P Lerp(P from, P to, double amount)
    {
        Vector256<double> fromVector = from.ToDoubleVector();
        Vector256<double> toVector = to.ToDoubleVector();
        return P.FromDoubleVector(fromVector + ((toVector - fromVector) * amount));
    }
}

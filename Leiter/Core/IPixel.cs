namespace Leiter.Core;

/// <summary>
/// Represents the value of an element in a matrix made up of one or more primative fields; usually a pixel in an image.
/// </summary>
/// <typeparam name="P">The type of the pixel.</typeparam>
/// <typeparam name="T">The primitive type that makes makes up this pixel.</typeparam>
public interface IPixel<P> : ISelfOperable<P>, INumericOperable<P>, IScalarOperable<P>
    where P : struct, IPixel<P>
{
    double Distance(P otherPixel);
}
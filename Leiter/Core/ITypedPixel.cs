namespace Leiter.Core;

/// <summary>
/// Represents the value of an element in a matrix made up of one or more primative fields; usually a pixel in an image.
/// </summary>
/// <typeparam name="P">The type of the pixel.</typeparam>
/// <typeparam name="T">The primitive type that makes makes up this pixel.</typeparam>
public interface ITypedPixel<P, T> : IPixel<P>, IEnumerable<T>
    where P : struct, ITypedPixel<P, T>
    where T : unmanaged, IComparable, IFormattable, IConvertible, IComparable<T>, IEquatable<T>
{
    /// <summary>
    /// Performs a component-wise map operation on this pixel across all of the fields it contains.
    /// </summary>
    /// <param name="func">The function that perform the map operation.</param>
    /// <returns>A new <see cref="ITypedPixel{P,T}"/> of type <c>P</c> with the map operation applied.</returns>
    P ComponentMap(Func<T, T> func);

    /// <summary>
    /// Performs a component-wise map operation on only the _color_ components of this pixel.
    /// </summary>
    /// <param name="func">The function that perform the map operation.</param>
    /// <returns>A new <see cref="ITypedPixel{P,T}"/> of type <c>P</c> with the map operation applied.</returns>
    P ColorComponentMap(Func<T, T> func);
}
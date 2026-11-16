namespace Leiter.Pixels.ColorSpaces;

/// <summary>
/// Enumeration of CIE standard reference white illuminants used for XYZ ↔ CIELAB conversions.
/// </summary>
public enum Illuminant
{
    /// <summary>
    /// CIE Standard Illuminant D65 (X = 0.9504, Y = 1.0000, Z = 1.0888), the standard daylight white point for sRGB.
    /// </summary>
    D65,

    /// <summary>
    /// CIE Standard Illuminant E (X = 1.0000, Y = 1.0000, Z = 1.0000), the equal-energy white point recommended for CIELAB color transfer.
    /// </summary>
    E
}

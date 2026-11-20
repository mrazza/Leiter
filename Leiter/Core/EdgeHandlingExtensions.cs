namespace Leiter.Core;

internal static class EdgeHandlingExtensions
{
    public static int ResolveIndex(this EdgeHandling edgeHandling, int index, int length) => edgeHandling switch
    {
        EdgeHandling.Extend => Math.Clamp(index, 0, length - 1),
        _ => throw new ArgumentOutOfRangeException(nameof(edgeHandling), edgeHandling, $"Unsupported {nameof(EdgeHandling)} value.")
    };

    public static double ResolveCoordinate(this EdgeHandling edgeHandling, double coord, int length) => edgeHandling switch
    {
        EdgeHandling.Extend => Math.Clamp(coord, 0.0, length - 1),
        _ => throw new ArgumentOutOfRangeException(nameof(edgeHandling), edgeHandling, $"Unsupported {nameof(EdgeHandling)} value.")
    };
}

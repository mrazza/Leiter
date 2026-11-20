namespace Leiter.Algorithms.Segmentation.Internal;

using System.Runtime.CompilerServices;
using Leiter.Pixels;

/// <summary>
/// Accumulates unweighted or weighted 5D spatial-CIELAB Mean-Shift coordinates for mode averaging.
/// </summary>
internal struct MeanShiftModeAccumulator
{
    public double WeightSum;
    public double WeightedX;
    public double WeightedY;
    public double WeightedL;
    public double WeightedA;
    public double WeightedB;
    public int Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(double x, double y, Lab32 color)
    {
        WeightedX += x;
        WeightedY += y;
        WeightedL += color.L;
        WeightedA += color.A;
        WeightedB += color.B;
        WeightSum += 1.0;
        Count++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddWeighted(MeanShiftMode mode, double weight)
    {
        WeightedX += weight * mode.X;
        WeightedY += weight * mode.Y;
        WeightedL += weight * mode.Color.L;
        WeightedA += weight * mode.Color.A;
        WeightedB += weight * mode.Color.B;
        WeightSum += weight;
        Count++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryGetMode(out MeanShiftMode mode)
    {
        if (WeightSum <= 0.0)
        {
            mode = default;
            return false;
        }

        double invWeight = 1.0 / WeightSum;
        mode = new MeanShiftMode(
            WeightedX * invWeight,
            WeightedY * invWeight,
            new Lab32((float)(WeightedL * invWeight), (float)(WeightedA * invWeight), (float)(WeightedB * invWeight)));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly MeanShiftMode ToModeOrFallback(MeanShiftMode fallback) =>
        TryGetMode(out MeanShiftMode mode) ? mode : fallback;
}

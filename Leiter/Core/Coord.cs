namespace Leiter.Core;

public readonly record struct Coord(int X, int Y)
{
    public static Coord operator +(Coord a, Coord b) => new(a.X + b.X, a.Y + b.Y);
}
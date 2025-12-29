namespace Leiter.Core;

public readonly record struct Coord(int X, int Y)
{
    public readonly double Distance(Coord other) =>
        Math.Sqrt(Math.Pow(this.X - other.X, 2) + Math.Pow(this.Y - other.Y, 2));

    public static Coord operator +(Coord left, Coord right) => new(left.X + right.X, left.Y + right.Y);
}
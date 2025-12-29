namespace Leiter.Core;

public readonly record struct NormalizedCoord(double X, double Y)
{
    public readonly double Distance(NormalizedCoord other) =>
        Math.Sqrt(Math.Pow(this.X - other.X, 2) + Math.Pow(this.Y - other.Y, 2));

    public static NormalizedCoord operator +(NormalizedCoord left, NormalizedCoord right) => new(left.X + right.X, left.Y + right.Y);

    public static NormalizedCoord operator -(NormalizedCoord left, double right) => new(left.X - right, left.Y - right);
}
namespace Leiter.Core;

public interface IUntypedMatrix
{
    int Width { get; }
    int Height { get; }

    Size Size { get; }

    int Count { get; }
}
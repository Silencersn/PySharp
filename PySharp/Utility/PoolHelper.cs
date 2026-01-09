using System.Buffers;

namespace PySharp.Utility;

internal static class PoolHelper
{
    public static Span<T> Rent<T>(int length, out T[] arrayToReturn)
    {
        return (arrayToReturn = ArrayPool<T>.Shared.Rent(length)).AsSpan()[..length];
    }

    public static void ReturnIfNonNull<T>(T[]? arrayToReturn)
    {
        if (arrayToReturn is null)
            return;

        ArrayPool<T>.Shared.Return(arrayToReturn);
    }
}

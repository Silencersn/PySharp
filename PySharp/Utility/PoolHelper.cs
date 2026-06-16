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

    public static RentedArray<T> Rent<T>(int length)
    {
        return new RentedArray<T>(length);
    }

    internal readonly ref struct RentedArray<T> : IDisposable
    {
        private readonly T[] _array;
        internal Span<T> Span { get; }

        public RentedArray(int length)
        {
            Rent(length, out _array);
            Span = _array.AsSpan()[..length];
        }

        void IDisposable.Dispose()
        {
            ArrayPool<T>.Shared.Return(_array);
        }
    }
}

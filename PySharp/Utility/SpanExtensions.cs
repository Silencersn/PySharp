using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PySharp.Utility;

internal static class SpanExtensions
{
    public static bool ContainsRef<T>(this Span<T> span, ref T value)
    {
        ref T start = ref MemoryMarshal.GetReference(span);
        nint byteOffset = Unsafe.ByteOffset(ref start, ref value);
        nuint byteLength = (nuint)((nint)span.Length * Unsafe.SizeOf<T>());
        return (nuint)byteOffset < byteLength;
    }

    public static Span<TTo> Cast<TFrom, TTo>(this Span<TFrom> span)
    {
        Debug.Assert(Unsafe.SizeOf<TFrom>() == Unsafe.SizeOf<TTo>());
        return Unsafe.As<Span<TFrom>, Span<TTo>>(ref span);
    }
}

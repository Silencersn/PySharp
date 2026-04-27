using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

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
}

using PySharp.PyModules.Builtins;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace PySharp.Utility;

internal static class UnsafeUtils
{
    public static Span<TTo> CastSpan<TFrom, TTo>(Span<TFrom> span) where TTo : class, TFrom
    {
        return Unsafe.BitCast<Span<TFrom>, Span<TTo>>(span);
    }
    public static ReadOnlySpan<TTo> CastReadOnlySpan<TFrom, TTo>(ReadOnlySpan<TFrom> span) where TTo : class, TFrom
    {
        return Unsafe.BitCast<ReadOnlySpan<TFrom>, ReadOnlySpan<TTo>>(span);
    }
}

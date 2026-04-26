using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PySharp.Utility;

internal static class ArrayStackHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Push<T>(ref T[] array, ref int length, in T item)
    {
        Debug.Assert(array is not null);
        Debug.Assert(length >= 0);
        Debug.Assert(length <= array.Length);

        if (array.Length == length)
            Array.Resize(ref array, length is 0 ? 4 : length * 2);
        array[length++] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PushPooled<T>(ref T[] array, ref int length, in T item)
    {
        Debug.Assert(array is not null);
        Debug.Assert(length >= 0);
        Debug.Assert(length <= array.Length);

        if (array.Length == length)
        {
            int newSize = length == 0 ? 4 : length * 2;
            T[] newArray = ArrayPool<T>.Shared.Rent(newSize);
            Array.Copy(array, newArray, length);
            ArrayPool<T>.Shared.Return(array, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            array = newArray;
        }

        array[length++] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Pop<T>(T[] array, ref int length)
    {
        Debug.Assert(array is not null);
        Debug.Assert(length > 0);
        Debug.Assert(length <= array.Length);

        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            return array[--length];

        var value = array[--length];
        array[length] = default!;
        return value;
    }
}

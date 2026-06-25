using PySharp.Modules.Builtins;

namespace PySharp.Runtime;

/// <summary>
/// Describes the internal structure of a buffer exposed by buffer protocol objects.
/// Corresponds to CPython's <c>Py_buffer</c> struct.
/// Used internally by memoryview to track the layout of the underlying data.
/// <para/>
/// MVP: only supports 1D byte format ('B') for bytes and bytearray objects.
/// </summary>
[AIGenerated]
internal sealed class PyBuffer
{
    public PyObject Object { get; }
    public bool ReadOnly { get; }
    public int ItemSize { get; }
    public string Format { get; }
    public int NumberDimensions { get; }
    public nint[] Shape { get; }
    public nint[] Strides { get; }

    public PyBuffer(PyObject obj, bool readOnly, int itemSize, string format,
                    int numberDimensions, nint[] shape, nint[] strides)
    {
        Object = obj;
        ReadOnly = readOnly;
        ItemSize = itemSize;
        Format = format;
        NumberDimensions = numberDimensions;
        Shape = shape;
        Strides = strides;
    }

    /// <summary>
    /// Total number of bytes in the buffer = product(shape) * itemsize.
    /// </summary>
    public nint Length
    {
        get
        {
            nint total = ItemSize;
            foreach (var dim in Shape)
                total *= dim;
            return total;
        }
    }

    /// <summary>
    /// Whether the buffer is C-contiguous (row-major).
    /// </summary>
    public bool CContiguous
    {
        get
        {
            if (NumberDimensions <= 1) return true;
            nint expected = ItemSize;
            for (int i = 0; i < NumberDimensions; i++)
            {
                if (Strides[i] != expected) return false;
                expected *= Shape[i];
            }
            return true;
        }
    }

    /// <summary>
    /// Whether the buffer is Fortran-contiguous (column-major).
    /// </summary>
    public bool FContiguous
    {
        get
        {
            if (NumberDimensions <= 1) return true;
            nint expected = ItemSize;
            for (int i = NumberDimensions - 1; i >= 0; i--)
            {
                if (Strides[i] != expected) return false;
                expected *= Shape[i];
            }
            return true;
        }
    }

    public bool Contiguous => CContiguous || FContiguous;
}

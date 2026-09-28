using PySharp.Modules.Builtins;

namespace PySharp.Runtime;

/// <summary>
/// Describes the internal structure of a buffer exposed by buffer protocol objects.
/// Corresponds to CPython's <c>Py_buffer</c> struct.
/// Used internally by memoryview to track the layout of the underlying data.
/// <para/>
/// MVP: only supports 1D byte format ('B') for bytes and bytearray objects.
/// The view reads and writes the exporter's live storage through
/// <see cref="Object"/>; <see cref="Offset"/> locates the view's first byte.
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
    public nint Offset { get; }

    public PyBuffer(PyObject obj, bool readOnly, int itemSize, string format,
                    int numberDimensions, nint[] shape, nint[] strides, nint offset = 0)
    {
        Object = obj;
        ReadOnly = readOnly;
        ItemSize = itemSize;
        Format = format;
        NumberDimensions = numberDimensions;
        Shape = shape;
        Strides = strides;
        Offset = offset;
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
            // PyBuffer_IsContiguous: 1-D (and 0-dim) is C-contiguous when the
            // sole stride equals the item size or the dimension holds a single
            // element — a multi-element strided subview reports False, empty
            // or not
            if (NumberDimensions <= 1)
                return Shape.Length is 0 || Shape[0] is 1 || Strides[0] == ItemSize;
            nint expected = ItemSize;
            for (int i = 0; i < NumberDimensions; i++)
            {
                if (Strides[i] != expected)
                    return false;
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
            if (NumberDimensions <= 1)
                return Shape.Length is 0 || Shape[0] is 1 || Strides[0] == ItemSize;
            nint expected = ItemSize;
            for (int i = NumberDimensions - 1; i >= 0; i--)
            {
                if (Strides[i] != expected)
                    return false;
                expected *= Shape[i];
            }
            return true;
        }
    }

    public bool Contiguous => CContiguous || FContiguous;
}

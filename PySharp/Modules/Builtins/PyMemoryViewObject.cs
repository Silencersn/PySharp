using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Globalization;
using System.Text;

namespace PySharp.Modules.Builtins;

/// <summary>
/// Represents a Python <c>memoryview</c> object that exposes the internal data
/// of an object supporting the buffer protocol without copying.
/// <para/>
/// MVP: only supports 1D byte format ('B') for bytes and bytearray objects.
/// The view holds no data of its own: element reads and writes go straight to
/// the exporter's live storage through <see cref="Buffer"/>, so writes through
/// the view land in the exporter and exporter mutations are observed by the
/// view. Creating a view counts as one buffer export on bytearray exporters,
/// which blocks their length-changing operations with <c>BufferError</c> until
/// the view is released.
/// </summary>
[AIGenerated]
public sealed class PyMemoryViewObject : PyObject
{
    private readonly PyBuffer _buffer;
    private bool _released;

    public override PyTypeObject DefaultPyType => PyMemoryViewObjectType.Shared;
    internal override bool IsImmutable => _buffer.ReadOnly;

    internal PyMemoryViewObject(PyBuffer buffer)
    {
        _buffer = buffer;
        _released = false;
        if (buffer.Object is PyByteArrayObject byteArray)
            byteArray.AddExport();
    }

    // --- Public accessors ---

    public bool Released => _released;

    internal PyBuffer Buffer => _buffer;
    internal PyObject Object => _buffer.Object;
    internal bool ReadOnly => _buffer.ReadOnly;
    internal int ItemSize => _buffer.ItemSize;
    internal string Format => _buffer.Format;
    internal int NumberDimensions => _buffer.NumberDimensions;
    internal nint[] Shape => _buffer.Shape;
    internal nint[] Strides => _buffer.Strides;
    internal nint Offset => _buffer.Offset;
    internal nint Length => _buffer.Length;
    internal bool CContiguous => _buffer.CContiguous;
    internal bool FContiguous => _buffer.FContiguous;
    internal bool Contiguous => _buffer.Contiguous;

    // --- Live data channel ---

    // The element stride in bytes; every element access adds it on top of the
    // view's base offset so strided subviews keep addressing the exporter
    private nint ElementByteOffset(nint elementIndex)
    {
        return _buffer.Offset + elementIndex * _buffer.Strides[0];
    }

    internal byte ReadElement(nint elementIndex)
    {
        return ReadByteAt(ElementByteOffset(elementIndex));
    }

    internal void WriteElement(nint elementIndex, byte value)
    {
        WriteByteAt(ElementByteOffset(elementIndex), value);
    }

    private ReadOnlySpan<byte> ExporterSpan
    {
        get
        {
            return Object switch
            {
                PyByteArrayObject byteArray => byteArray.AsSpan(),
                PyBytesObject bytes => bytes.AsSpan(),
                // NewImpl only admits bytes and bytearray exporters
                _ => throw new InvalidOperationException("memoryview exporter is neither bytes nor bytearray"),
            };
        }
    }

    private byte ReadByteAt(nint byteOffset)
    {
        return ExporterSpan[(int)byteOffset];
    }

    private void WriteByteAt(nint byteOffset, byte value)
    {
        // Only a bytearray exporter can be writable; NewImpl marks bytes
        // views read-only and the write paths check ReadOnly first
        ((PyByteArrayObject)Object).AsMutableSpan()[(int)byteOffset] = value;
    }

    // Live view of the buffer contents as a contiguous byte sequence: a
    // direct window when the layout is contiguous, otherwise gathered
    // element by element. Every call re-reads the exporter.
    internal ReadOnlySpan<byte> DataSpan
    {
        get
        {
            var total = checked((int)Length);
            var source = ExporterSpan;
            if (_buffer.Strides[0] == _buffer.ItemSize)
                return source.Slice((int)_buffer.Offset, total);

            var gathered = new byte[total];
            for (nint i = 0; i < total / _buffer.ItemSize; i++)
            {
                var src = (int)(_buffer.Offset + i * _buffer.Strides[0]);
                source.Slice(src, _buffer.ItemSize).CopyTo(gathered.AsSpan((int)(i * _buffer.ItemSize), _buffer.ItemSize));
            }

            return gathered;
        }
    }

    // --- Release ---

    // CPython 3.14 memory_release is idempotent: releasing a released view
    // returns None. Each view object owns exactly one export, dropped here.
    internal void DoRelease()
    {
        if (_released)
            return;
        _released = true;
        if (Object is PyByteArrayObject byteArray)
            byteArray.RemoveExport();
    }

    // --- Index/slice helpers ---

    internal PyResult? TryMapIndex(int index, out int mappedIndex)
    {
        mappedIndex = 0;
        if (_released)
            return PyResult.ValueError("operation forbidden on released memoryview object");
        if (_buffer.NumberDimensions is 0)
            return PyResult.TypeError("0-dim memory has no length");
        if (_buffer.Shape.Length is 0 || _buffer.Shape[0] is 0)
            return PyResult.IndexError("index out of bounds");

        var len = (int)_buffer.Shape[0];
        mappedIndex = index < 0 ? index + len : index;
        if (mappedIndex < 0 || mappedIndex >= len)
            return PyResult.IndexError("index out of bounds");
        return null;
    }

    internal PyResult? CheckReleased()
    {
        if (_released)
            return PyResult.ValueError("operation forbidden on released memoryview object");
        return null;
    }
}


// ====================================================================
// Type class
// ====================================================================

[AIGenerated]
[PyType("memoryview")]
public sealed partial class PyMemoryViewObjectType : PyTypeObject<PyMemoryViewObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    // --- Constructor: memoryview(object) ---

    [PyFunctionParameters("object", "/")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var obj = arguments[0];

        // memoryview(memoryview): re-exports the same buffer, so .obj stays
        // the original exporter (CPython memoryview_new) and the new view
        // holds its own export
        if (obj is PyMemoryViewObject mv)
        {
            var err = mv.CheckReleased();
            if (err is not null)
                return err.Value;
            return new PyMemoryViewObject(mv.Buffer);
        }

        // memoryview(bytes) — readonly
        if (obj is PyBytesObject bytes)
        {
            var buffer = new PyBuffer(
                bytes, readOnly: true, itemSize: 1, format: "B",
                numberDimensions: 1, shape: [bytes.Length], strides: [1]);
            return new PyMemoryViewObject(buffer);
        }

        // memoryview(bytearray) — a live view over the bytearray's storage;
        // the constructor registers the export that blocks resizes
        if (obj is PyByteArrayObject byteArray)
        {
            var buffer = new PyBuffer(
                byteArray, readOnly: false, itemSize: 1, format: "B",
                numberDimensions: 1, shape: [byteArray.Length], strides: [1]);
            return new PyMemoryViewObject(buffer);
        }

        // Unsupported type
        return PyResult.TypeError("memoryview: a bytes-like object is required, not '{0}'",
            obj.PyType.TpName);
    }

    // --- __new__ ---

    protected override PyResult New(PyCallContext context, PyTypeObject cls,
        IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    // --- __enter__ / __exit__ ---

    protected override PyResult Enter(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return self;
    }

    protected override PyResult Exit(PyCallContext context, PyMemoryViewObject self,
        PyObject excType, PyObject excValue, PyObject excTraceback)
    {
        self.DoRelease();
        return PyNoneObject.None;
    }

    // --- __repr__: <memory at 0x...> ---

    protected override PyResult Repr(PyCallContext context, PyMemoryViewObject self)
    {
        if (self.Released)
            return PyStrObject.FromString($"<released memory at 0x{self.PyId:X16}>");
        return PyStrObject.FromString($"<memory at 0x{self.PyId:X16}>");
    }

    // --- __str__ ---

    protected override PyResult Str(PyCallContext context, PyMemoryViewObject self)
    {
        return Repr(context, self);
    }

    // --- __len__ ---

    protected override PyResult Len(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;

        if (self.NumberDimensions is 0)
            return PyResult.TypeError("0-dim memory has no length");
        return PyIntObject.FromInteger((int)self.Shape[0]);
    }

    // --- __getitem__ ---

    protected override PyResult GetItem(PyCallContext context, PyMemoryViewObject self, PyObject item)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;

        // Slice → subview over the same exporter, no copy: the subview
        // advances the base offset and multiplies the stride by the step
        if (item is PySliceObject slice)
        {
            if (self.NumberDimensions is 0)
                return PyResult.TypeError("0-dim memory has no length");

            var indicesResult = slice.Indices(context, (int)self.Shape[0], out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (start, _, step, length) = indices;

            var sliceBuffer = new PyBuffer(
                self.Object, self.ReadOnly, self.ItemSize, self.Format,
                self.NumberDimensions, [length], [self.Strides[0] * step],
                offset: self.Offset + start * self.Strides[0]);
            return new PyMemoryViewObject(sliceBuffer);
        }

        // Integer index → element value
        var indexResult = PySpecialMethods.Index(context, item);
        if (indexResult.IsError)
            return indexResult;

        var mapErr = self.TryMapIndex(indexResult.Value.Int32Value, out var idx);
        if (mapErr is not null)
            return mapErr.Value;

        return PyIntObject.FromInteger(self.ReadElement(idx));
    }

    // --- __setitem__ ---

    protected override PyResult SetItem(PyCallContext context, PyMemoryViewObject self,
        PyObject key, PyObject value)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;

        if (self.ReadOnly)
            return PyResult.TypeError("cannot modify read-only memory");

        if (key is PySliceObject slice)
        {
            // memory_ass_subscript takes the value's buffer before resolving
            // the slice: a released view is inaccessible and refuses with
            // ValueError, a non-buffer rvalue with TypeError
            if (value is PyMemoryViewObject { Released: true })
                return PyResult.ValueError("operation forbidden on released memoryview object");

            if (!PyBytesObjectType.TryGetBytesLikeSpan(value, out var valueSpan))
                return PyResult.TypeError(PySR.Runtime_Bytes_BytesLikeRequired, value.PyType.TpName);

            if (self.NumberDimensions is 0)
                return PyResult.TypeError("0-dim memory has no length");

            var indicesResult = slice.Indices(context, (int)self.Shape[0], out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (start, _, step, sliceLength) = indices;

            if (valueSpan.Length != sliceLength * self.ItemSize)
                return PyResult.ValueError(PySR.Runtime_Memoryview_AssignmentStructureMismatch);

            // CPython writes with memmove semantics: snapshot the rvalue so a
            // live window sharing the exporter's storage (a contiguous view,
            // the bytearray itself) is not corrupted by a forward overlap
            var snapshot = valueSpan.ToArray();
            for (var i = 0; i < sliceLength; i++)
                self.WriteElement(start + i * step, snapshot[i * self.ItemSize]);

            return PyNoneObject.None;
        }

        var indexResult = PySpecialMethods.Index(context, key);
        if (indexResult.IsError)
            return indexResult;

        var mapErr = self.TryMapIndex(indexResult.Value.Int32Value, out var idx);
        if (mapErr is not null)
            return mapErr.Value;

        var valueIndexResult = PySpecialMethods.Index(context, value);
        if (valueIndexResult.IsError)
        {
            // pack_single rewrites a failed conversion with the format-
            // specific TypeError; other failures propagate untouched
            if (PyTypeErrorObjectType.Shared.IsInstance(valueIndexResult.Exception))
                return PyResult.TypeError(PySR.Runtime_Memoryview_InvalidFormatType, self.Format);
            return valueIndexResult;
        }

        var byteValue = valueIndexResult.Value.Value;
        // pack_single: format 'b' is a signed char, 'B'/'c' are unsigned
        var inRange = self.Format is "b"
            ? byteValue >= sbyte.MinValue && byteValue <= sbyte.MaxValue
            : byteValue >= byte.MinValue && byteValue <= byte.MaxValue;
        if (!inRange)
            return PyResult.ValueError(PySR.Runtime_Memoryview_InvalidFormatValue, self.Format);

        self.WriteElement(idx, (byte)byteValue);

        return PyNoneObject.None;
    }

    // --- __iter__ ---

    protected override PyResult Iter(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return new PyMemoryViewIteratorObject(self);
    }

    // --- __eq__ / __ne__ ---

    protected override PyResult Eq(PyCallContext context, PyMemoryViewObject self, PyObject other)
    {
        // CPython 3.14 memory_richcompare: an inaccessible (released) operand
        // short-circuits the whole comparison to identity instead of raising
        if (self.Released || other is PyMemoryViewObject { Released: true })
            return PyBoolObject.FromBoolean(ReferenceEquals(self, other));

        if (other is PyMemoryViewObject otherMv)
        {
            if (self.NumberDimensions != otherMv.NumberDimensions)
                return PyBoolObject.False;
            return PyBoolObject.FromBoolean(self.DataSpan.SequenceEqual(otherMv.DataSpan));
        }

        if (other is PyBytesObject bytes)
            return PyBoolObject.FromBoolean(self.DataSpan.SequenceEqual(bytes.AsSpan()));

        if (other is PyByteArrayObject ba)
            return PyBoolObject.FromBoolean(self.DataSpan.SequenceEqual(ba.AsSpan()));

        return PyNotImplementedObject.NotImplemented;
    }

    // --- __hash__ ---

    protected override PyResult Hash(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;

        if (!self.ReadOnly)
            return PyResult.ValueError("cannot hash writable memoryview object");
        if (self.Format is not "B" and not "b" and not "c")
            return PyResult.ValueError("cannot hash memoryview object with format '{0}'", self.Format);
        if (self.NumberDimensions is not 1)
            return PyResult.ValueError("cannot hash multi-dimensional memoryview object");

        unchecked
        {
            int hash = (int)self.Shape[0];
            foreach (byte b in self.DataSpan)
                hash = hash * 31 + b;
            return PyIntObject.FromInteger(hash);
        }
    }

    // --- __contains__ ---

    protected override PyResult Contains(PyCallContext context, PyMemoryViewObject self, PyObject item)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;

        var indexResult = PySpecialMethods.Index(context, item);
        if (indexResult.IsError)
            return indexResult;

        var val = (byte)(indexResult.Value.Value & 0xFF);
        return PyBoolObject.FromBoolean(self.DataSpan.Contains(val));
    }

    // --- Methods ---

    [PyMethod("tobytes")]
    [PyFunctionParameters()]
    private static PyResult ToBytes(PyCallContext context, PyMemoryViewObject self, PyArguments arguments)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return PyBytesObject.FromBytes(self.DataSpan);
    }

    [PyMethod("tolist")]
    [PyFunctionParameters()]
    private static PyResult ToList(PyCallContext context, PyMemoryViewObject self, PyArguments arguments)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;

        var span = self.DataSpan;
        var list = new PyObject[span.Length];
        for (int i = 0; i < span.Length; i++)
            list[i] = PyIntObject.FromInteger(span[i]);

        return PyListObject.CreateList(list);
    }

    [PyMethod("hex")]
    [PyFunctionParameters()]
    private static PyResult Hex(PyCallContext context, PyMemoryViewObject self, PyArguments arguments)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;

        var builder = new StringBuilder(self.DataSpan.Length * 2);
        foreach (byte b in self.DataSpan)
            builder.AppendFormat(CultureInfo.InvariantCulture, "{0:x2}", b);
        return PyStrObject.FromString(builder.ToString());
    }

    [PyMethod("release")]
    [PyFunctionParameters()]
    private static PyResult Release(PyCallContext context, PyMemoryViewObject self, PyArguments arguments)
    {
        self.DoRelease();
        return PyNoneObject.None;
    }

    [PyMethod("toreadonly")]
    [PyFunctionParameters()]
    private static PyResult ToReadOnly(PyCallContext context, PyMemoryViewObject self, PyArguments arguments)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;

        var roBuffer = new PyBuffer(
            self.Object, readOnly: true, self.ItemSize, self.Format,
            self.NumberDimensions, self.Shape, self.Strides, offset: self.Offset);
        return new PyMemoryViewObject(roBuffer);
    }

    // --- Attribute getters ---

    [PyProperty("obj")]
    private static PyResult GetObject(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return self.Object;
    }

    [PyProperty("nbytes")]
    private static PyResult GetLength(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return PyIntObject.FromInteger(self.Length);
    }

    [PyProperty("readonly")]
    private static PyResult GetReadOnly(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return PyBoolObject.FromBoolean(self.ReadOnly);
    }

    [PyProperty("format")]
    private static PyResult GetFormat(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return PyStrObject.FromString(self.Format);
    }

    [PyProperty("itemsize")]
    private static PyResult GetItemSize(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return PyIntObject.FromInteger(self.ItemSize);
    }

    [PyProperty("ndim")]
    private static PyResult GetNumberDimensions(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return PyIntObject.FromInteger(self.NumberDimensions);
    }

    [PyProperty("shape")]
    private static PyResult GetShape(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;

        var items = new PyObject[self.Shape.Length];
        for (int i = 0; i < self.Shape.Length; i++)
            items[i] = PyIntObject.FromInteger(self.Shape[i]);
        return PyTupleObject.CreateTuple(items);
    }

    [PyProperty("strides")]
    private static PyResult GetStrides(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;

        var items = new PyObject[self.Strides.Length];
        for (int i = 0; i < self.Strides.Length; i++)
            items[i] = PyIntObject.FromInteger(self.Strides[i]);
        return PyTupleObject.CreateTuple(items);
    }

    [PyProperty("suboffsets")]
    private static PyResult GetSubOffsets(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return PyTupleObject.CreateTuple([]);
    }

    [PyProperty("c_contiguous")]
    private static PyResult GetCContiguous(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return PyBoolObject.FromBoolean(self.CContiguous);
    }

    [PyProperty("f_contiguous")]
    private static PyResult GetFContiguous(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return PyBoolObject.FromBoolean(self.FContiguous);
    }

    [PyProperty("contiguous")]
    private static PyResult GetContiguous(PyCallContext context, PyMemoryViewObject self)
    {
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;
        return PyBoolObject.FromBoolean(self.Contiguous);
    }
}

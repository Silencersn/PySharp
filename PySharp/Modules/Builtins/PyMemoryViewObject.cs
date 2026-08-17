using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Text;

namespace PySharp.Modules.Builtins;

/// <summary>
/// Represents a Python <c>memoryview</c> object that exposes the internal data
/// of an object supporting the buffer protocol without copying.
/// <para/>
/// MVP: only supports 1D byte format ('B') for bytes and bytearray objects.
/// </summary>
[AIGenerated]
public sealed class PyMemoryViewObject : PyObject
{
    private readonly PyBuffer _buffer;
    private readonly byte[] _data;
    private bool _released;

    public override PyTypeObject DefaultPyType => PyMemoryViewObjectType.Shared;
    internal override bool IsImmutable => _buffer.ReadOnly;

    internal PyMemoryViewObject(PyBuffer buffer, byte[] data)
    {
        _buffer = buffer;
        _data = data;
        _released = false;
    }

    // --- Public accessors ---

    public bool Released => _released;

    internal PyBuffer Buffer => _buffer;
    internal PyObject Object => _buffer.Object;
    internal ReadOnlySpan<byte> DataSpan => _data.AsSpan();
    internal byte[] DataArray => _data;
    internal bool ReadOnly => _buffer.ReadOnly;
    internal int ItemSize => _buffer.ItemSize;
    internal string Format => _buffer.Format;
    internal int NumberDimensions => _buffer.NumberDimensions;
    internal nint[] Shape => _buffer.Shape;
    internal nint[] Strides => _buffer.Strides;
    internal nint Length => _buffer.Length;
    internal bool CContiguous => _buffer.CContiguous;
    internal bool FContiguous => _buffer.FContiguous;
    internal bool Contiguous => _buffer.Contiguous;
    internal nint[] SubOffsets => [];

    // --- Release ---

    internal void DoRelease()
    {
        _released = true;
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

        // memoryview(memoryview)
        if (obj is PyMemoryViewObject mv)
        {
            var err = mv.CheckReleased();
            if (err is not null)
                return err.Value;
            return new PyMemoryViewObject(mv.Buffer, mv.DataArray);
        }

        // memoryview(bytes) — readonly
        if (obj is PyBytesObject bytes)
        {
            var buffer = new PyBuffer(
                bytes, readOnly: true, itemSize: 1, format: "B",
                numberDimensions: 1, shape: [bytes.Length], strides: [1]);
            return new PyMemoryViewObject(buffer, bytes.AsSpan().ToArray());
        }

        // memoryview(bytearray) — snapshot-based for MVP
        if (obj is PyByteArrayObject byteArray)
        {
            var buffer = new PyBuffer(
                byteArray, readOnly: false, itemSize: 1, format: "B",
                numberDimensions: 1, shape: [byteArray.Length], strides: [1]);
            return new PyMemoryViewObject(buffer, byteArray.AsSpan().ToArray());
        }

        // Unsupported type
        return PyResult.TypeError("memoryview: a bytes-like object is required, not '{0}'",
            obj.PyType.FullName);
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

        // Slice → subview
        if (item is PySliceObject slice)
        {
            if (self.NumberDimensions is 0)
                return PyResult.TypeError("0-dim memory has no length");

            var indicesResult = slice.Indices(context, (int)self.Shape[0], out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (start, _, step, length) = indices;
            if (length is 0)
            {
                return new PyMemoryViewObject(new PyBuffer(
                    self.Object, self.ReadOnly, self.ItemSize, self.Format,
                    self.NumberDimensions, [0], [self.Strides[0]]), []);
            }

            var offset = start * self.ItemSize;
            var newData = new byte[length * self.ItemSize];
            var src = self.DataSpan;
            for (int i = 0; i < length; i++)
            {
                var srcIdx = offset + i * (int)self.Strides[0] * step;
                var dstIdx = i * self.ItemSize;
                src.Slice(srcIdx, self.ItemSize).CopyTo(newData.AsSpan(dstIdx));
            }

            var sliceBuffer = new PyBuffer(
                self.Object, self.ReadOnly, self.ItemSize, self.Format,
                self.NumberDimensions, [length], [self.ItemSize]);
            return new PyMemoryViewObject(sliceBuffer, newData);
        }

        // Integer index → element value
        var indexResult = PySpecialMethods.Index(context, item);
        if (indexResult.IsError)
            return indexResult;

        var mapErr = self.TryMapIndex(indexResult.Value.Int32Value, out var idx);
        if (mapErr is not null)
            return mapErr.Value;

        var byteVal = self.DataSpan[idx * self.ItemSize];

        if (self.Format is "B" or "b" or "c")
            return PyIntObject.FromInteger(byteVal);

        return PyIntObject.FromInteger(byteVal);
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

        if (key is PySliceObject)
            return PyResult.PySharpException("memoryview slice assignment not implemented");

        var indexResult = PySpecialMethods.Index(context, key);
        if (indexResult.IsError)
            return indexResult;

        var mapErr = self.TryMapIndex(indexResult.Value.Int32Value, out var idx);
        if (mapErr is not null)
            return mapErr.Value;

        var valueResult = PySpecialMethods.Index(context, value);
        if (valueResult.IsError)
            return valueResult;

        var byteVal = (byte)(valueResult.Value.Value & 0xFF);
        self.DataArray[idx] = byteVal;

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
        var err = self.CheckReleased();
        if (err is not null)
            return err.Value;

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

        var list = new PyObject[self.DataSpan.Length];
        for (int i = 0; i < self.DataSpan.Length; i++)
            list[i] = PyIntObject.FromInteger(self.DataSpan[i]);

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
            builder.AppendFormat("{0:x2}", b);
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
            self.NumberDimensions, self.Shape, self.Strides);
        return new PyMemoryViewObject(roBuffer, self.DataArray);
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

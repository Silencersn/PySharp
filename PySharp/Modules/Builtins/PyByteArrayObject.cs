using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Runtime.InteropServices;
using System.Text;

namespace PySharp.Modules.Builtins;

[AIGenerated]
public sealed class PyByteArrayObject : PyObject
{
    private readonly List<byte> _data;

    public override PyTypeObject DefaultPyType => PyByteArrayObjectType.Shared;

    public int Length => _data.Count;

    public byte this[int index]
    {
        get => _data[index];
        set => _data[index] = value;
    }

    private PyByteArrayObject(List<byte> data)
    {
        _data = data;
    }

    public static PyByteArrayObject CreateEmpty()
    {
        return new PyByteArrayObject([]);
    }

    public static PyByteArrayObject FromBytes(ReadOnlySpan<byte> data)
    {
        return new PyByteArrayObject([.. data]);
    }

    public static PyByteArrayObject FromBytes(IEnumerable<byte> data)
    {
        return new PyByteArrayObject([.. data]);
    }

    public ReadOnlySpan<byte> AsSpan()
    {
        return CollectionsMarshal.AsSpan(_data);
    }

    public void Add(byte value)
    {
        _data.Add(value);
    }

    public void AddRange(IEnumerable<byte> values)
    {
        _data.AddRange(values);
    }

    public void Clear()
    {
        _data.Clear();
    }

    public PyByteArrayObject Copy()
    {
        return new PyByteArrayObject([.. _data]);
    }

    public bool TrySetItem(int index, byte value)
    {
        if (index < 0 || index >= _data.Count)
            return false;

        _data[index] = value;
        return true;
    }

    public PyByteArrayObject Slice(int start, int step, int length)
    {
        if (length is 0)
            return CreateEmpty();

        var result = new List<byte>(length);
        for (int i = 0, j = start; i < length; i++, j += step)
            result.Add(_data[j]);

        return new PyByteArrayObject(result);
    }

    public void ReplaceSliceStep1(int start, int stop, List<byte> values)
    {
        int lower = int.Min(start, stop);
        int upper = int.Max(start, stop);
        _data.RemoveRange(lower, upper - lower);
        _data.InsertRange(lower, values);
    }

    public void ReplaceSliceStepN(int start, int step, int sliceLength, List<byte> values)
    {
        for (int i = 0, idx = start; i < sliceLength; i++, idx += step)
            _data[idx] = values[i];
    }

    public void RepeatInPlace(int n)
    {
        if (n <= 0)
        {
            _data.Clear();
            return;
        }

        if (n is 1)
            return;

        var source = _data.ToArray();
        _data.Capacity = source.Length * n;
        for (int i = 1; i < n; i++)
            _data.AddRange(source);
    }
}

[AIGenerated]
[PyType("bytearray")]
public sealed partial class PyByteArrayObjectType : PyTypeObject<PyByteArrayObject>
{
    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    [PyFunctionParameters("source=b''")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var source = arguments[0];
        if (source is PyByteArrayObject byteArray)
            return byteArray.Copy();

        if (source is PyBytesObject bytes)
            return PyByteArrayObject.FromBytes(bytes.AsSpan());

        if (source is PyStrObject)
            return PyResult.TypeError(PySR.Runtime_Bytes_StrWithoutEncoding);

        var listResult = PyUtils.IterableToList(context, source);
        if (listResult.IsError)
            return listResult;

        var data = new List<byte>(listResult.Value.Count);
        for (int i = 0; i < listResult.Value.Count; i++)
        {
            var item = listResult.Value[i];
            var byteResult = TryGetByteValue(context, item, out var b);
            if (byteResult.IsError)
                return byteResult;

            data.Add(b);
        }

        return PyByteArrayObject.FromBytes(data);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;

        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PyByteArrayObject self)
    {
        return PyStrObject.FromString($"bytearray({FormatBytesLiteral(self.AsSpan())})");
    }

    protected override PyResult Len(PyCallContext context, PyByteArrayObject self)
    {
        return PyIntObject.FromInteger(self.Length);
    }

    protected override PyResult GetItem(PyCallContext context, PyByteArrayObject self, PyObject item)
    {
        if (item is PySliceObject slice)
        {
            var (start, _, step, length) = slice.Indices(self.Length);
            return self.Slice(start, step, length);
        }

        var indexResult = PySpecialMethods.Index(context, item);
        if (indexResult.IsError)
            return indexResult;

        var index = Utils.MapIndex(indexResult.Value.Int32Value, self.Length);
        if (index < 0 || index >= self.Length)
            return PyResult.IndexError(PySR.Runtime_IndexOutOfRange);

        return PyIntObject.FromInteger(self[index]);
    }

    protected override PyResult SetItem(PyCallContext context, PyByteArrayObject self, PyObject key, PyObject value)
    {
        if (key is PySliceObject slice)
        {
            var (start, stop, step, sliceLength) = slice.Indices(self.Length);
            var valuesResult = TryGetByteList(context, value, out var values);
            if (valuesResult.IsError)
                return valuesResult;

            if (step != 1 && values.Count != sliceLength)
                return PyResult.ValueError(PySR.Runtime_Sequence_SliceStep_AssignWrongSize, sliceLength, values.Count);

            if (step == 1)
                self.ReplaceSliceStep1(start, stop, values);
            else
                self.ReplaceSliceStepN(start, step, sliceLength, values);

            return PyNoneObject.None;
        }

        var indexResult = PySpecialMethods.Index(context, key);
        if (indexResult.IsError)
            return indexResult;

        var mappedIndex = Utils.MapIndex(indexResult.Value.Int32Value, self.Length);
        var byteResult = TryGetByteValue(context, value, out var b);
        if (byteResult.IsError)
            return byteResult;

        if (!self.TrySetItem(mappedIndex, b))
            return PyResult.IndexError(PySR.Runtime_IndexOutOfRange);

        return PyNoneObject.None;
    }

    protected override PyResult Iter(PyCallContext context, PyByteArrayObject self)
    {
        return new PyByteArrayIteratorObject(self);
    }

    protected override PyResult Add(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        if (!TryGetSpan(other, out var otherSpan))
            return PyResult.TypeError("can't concat {0} to bytearray", other.PyType.FullName);

        var result = new byte[self.Length + otherSpan.Length];
        self.AsSpan().CopyTo(result);
        otherSpan.CopyTo(result.AsSpan()[self.Length..]);
        return PyByteArrayObject.FromBytes(result);
    }

    protected override PyResult IAdd(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        if (!TryGetSpan(other, out var otherSpan))
            return PyResult.TypeError("can't concat {0} to bytearray", other.PyType.FullName);

        self.AddRange(otherSpan.ToArray());
        return self;
    }

    protected override PyResult Mul(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        var indexResult = PySpecialMethods.Index(context, other);
        if (indexResult.IsError)
            return indexResult;

        var n = indexResult.Value.Value;
        if (n <= 0)
            return PyByteArrayObject.CreateEmpty();

        if (n == 1)
            return self.Copy();

        var intN = (int)n;
        var result = new byte[self.Length * intN];
        var srcSpan = self.AsSpan();
        var dstSpan = result.AsSpan();
        for (int i = 0; i < intN; i++)
            srcSpan.CopyTo(dstSpan[(i * srcSpan.Length)..]);

        return PyByteArrayObject.FromBytes(result);
    }

    protected override PyResult RMul(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        return Mul(context, self, other);
    }

    protected override PyResult IMul(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        var indexResult = PySpecialMethods.Index(context, other);
        if (indexResult.IsError)
            return indexResult;

        self.RepeatInPlace(indexResult.Value.Int32Value);
        return self;
    }

    protected override PyResult Eq(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        if (TryGetSpan(other, out var otherSpan))
            return PyBoolObject.FromBoolean(self.AsSpan().SequenceEqual(otherSpan));

        return PyNotImplementedObject.NotImplemented;
    }

    protected override PyResult Hash(PyCallContext context, PyByteArrayObject self)
    {
        return PyResult.TypeError(PySR.Runtime_Object_Unhashable, self.PyType.FullName);
    }

    [PyMethod("append")]
    [PyFunctionParameters("item", "/")]
    private static PyResult Append(PyCallContext context, PyByteArrayObject self, PyArguments arguments)
    {
        var byteResult = TryGetByteValue(context, arguments[0], out var b);
        if (byteResult.IsError)
            return byteResult;

        self.Add(b);
        return PyNoneObject.None;
    }

    [PyMethod("extend")]
    [PyFunctionParameters("iterable", "/")]
    private static PyResult Extend(PyCallContext context, PyByteArrayObject self, PyArguments arguments)
    {
        var valuesResult = TryGetByteList(context, arguments[0], out var values);
        if (valuesResult.IsError)
            return valuesResult;

        self.AddRange(values);
        return PyNoneObject.None;
    }

    private static bool TryGetSpan(PyObject source, out ReadOnlySpan<byte> span)
    {
        if (source is PyByteArrayObject byteArray)
        {
            span = byteArray.AsSpan();
            return true;
        }

        if (source is PyBytesObject bytes)
        {
            span = bytes.AsSpan();
            return true;
        }

        span = default;
        return false;
    }

    private static PyResult TryGetByteValue(PyCallContext context, PyObject item, out byte value)
    {
        value = default;
        var indexResult = PySpecialMethods.Index(context, item);
        if (indexResult.IsError)
            return indexResult;

        var intValue = indexResult.Value.Value;
        if (intValue < byte.MinValue || intValue > byte.MaxValue)
            return PyResult.ValueError(PySR.Runtime_Bytes_OutOfRange);

        value = (byte)intValue;
        return PyNoneObject.None;
    }

    private static PyResult TryGetByteList(PyCallContext context, PyObject iterable, out List<byte> result)
    {
        result = [];

        if (TryGetSpan(iterable, out var span))
        {
            result = [.. span.ToArray()];
            return PyNoneObject.None;
        }

        var listResult = PyUtils.IterableToList(context, iterable);
        if (listResult.IsError)
            return listResult;

        result = new List<byte>(listResult.Value.Count);
        for (int i = 0; i < listResult.Value.Count; i++)
        {
            var byteResult = TryGetByteValue(context, listResult.Value[i], out var b);
            if (byteResult.IsError)
                return byteResult;

            result.Add(b);
        }

        return PyNoneObject.None;
    }

    private static string FormatBytesLiteral(ReadOnlySpan<byte> span)
    {
        var containsSingle = span.Contains((byte)'\'');
        var containsDouble = span.Contains((byte)'\"');
        var wrapper = containsSingle && !containsDouble ? '"' : '\'';

        var builder = new StringBuilder("b").Append(wrapper);
        foreach (byte b in span)
        {
            if (b == wrapper)
                builder.Append('\\').Append(wrapper);
            else if (b == '\\')
                builder.Append("\\\\");
            else if (b is (byte)'\t')
                builder.Append("\\t");
            else if (b is (byte)'\r')
                builder.Append("\\r");
            else if (b is (byte)'\n')
                builder.Append("\\n");
            else if (b >= 0x20 && b <= 0x7E)
                builder.Append((char)b);
            else
                builder.AppendFormat("\\x{0:x2}", b);
        }

        builder.Append(wrapper);
        return builder.ToString();
    }
}

[AIGenerated]
[PyType("bytearray_iterator")]
public sealed partial class PyByteArrayIteratorObjectType : PyTypeObject<PyByteArrayIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PyByteArrayIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyByteArrayIteratorObject self)
    {
        return self.Next();
    }
}

[AIGenerated]
public sealed class PyByteArrayIteratorObject : PyObject
{
    private readonly PyByteArrayObject _byteArray;
    private int _index;

    public override PyTypeObject DefaultPyType => PyByteArrayIteratorObjectType.Shared;

    internal PyByteArrayIteratorObject(PyByteArrayObject byteArray)
    {
        _byteArray = byteArray;
        _index = 0;
    }

    internal PyResult Next()
    {
        if (_index >= _byteArray.Length)
            return PyResult.StopIteration();

        return PyIntObject.FromInteger(_byteArray[_index++]);
    }
}

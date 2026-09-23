using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Globalization;
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
    static PyByteArrayObjectType()
    {
        // CPython add_operators: unhashable types carry __hash__ = None in
        // the type dict (read face); the tp_hash override below raises
        Shared.PyAttributes[PySpecialNames.Hash] = PyNoneObject.None;
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython bytearray_new: the clinic parser reports arity, keyword
        // and str-converter failures before bytearray_new_impl sees the
        // arguments; a string source encodes, and non-string sources then
        // take the zero-fill / iterable / copy conversions
        var bindResult = PyBytesObjectType.BindCodecArguments("bytearray", args, kwargs, out var source, out var encoding, out var errors);
        if (bindResult.IsError)
            return bindResult;

        var encodingResult = PyBytesObjectType.CheckCodecStrArgument("bytearray", "encoding", encoding, out var encodingName);
        if (encodingResult.IsError)
            return encodingResult;

        var errorsResult = PyBytesObjectType.CheckCodecStrArgument("bytearray", "errors", errors, out var errorsName);
        if (errorsResult.IsError)
            return errorsResult;

        if (source is PyStrObject strSource)
        {
            if (encodingName is null)
                return PyResult.TypeError(PySR.Runtime_Bytes_StrWithoutEncoding);

            var encoded = PyStrObjectType.EncodeCore(context, strSource.Value, encodingName, errorsName ?? "strict");
            if (encoded.IsError)
                return encoded;

            return PyByteArrayObject.FromBytes(((PyBytesObject)encoded.Value).AsSpan());
        }

        if (encodingName is not null)
            return PyResult.TypeError(PySR.Runtime_Codec_EncodingWithoutString);
        if (errorsName is not null)
            return PyResult.TypeError(PySR.Runtime_Codec_ErrorsWithoutString);

        var obj = FromSource(context, source);
        if (obj.IsError)
            return obj;

        obj.Value._pyType = cls;
        return obj;
    }

    private static PyResult FromSource(PyCallContext context, PyObject? source)
    {
        if (source is null)
            return PyByteArrayObject.CreateEmpty();

        if (source is PyByteArrayObject byteArray)
            return byteArray.Copy();

        if (source is PyBytesObject bytes)
            return PyByteArrayObject.FromBytes(bytes.AsSpan());

        // CPython bytearray(n): an indexable source zero-fills n bytes
        // (PyNumber_Index); the iterable protocol is only the fallback
        if (source.PyType.Slots.Index is not null)
        {
            var indexResult = PySpecialMethods.Index(context, source);
            if (indexResult.IsError)
            {
                // CPython replaces a failed top-level conversion with its
                // own message; item-level errors later on are untouched
                if (PyTypeErrorObjectType.Shared.IsInstance(indexResult.Exception))
                    return PyResult.TypeError(PySR.Runtime_ByteArray_CannotConvert, source.PyType.TpName);

                return indexResult;
            }

            var count = indexResult.Value.Value;
            if (count < 0)
                return PyResult.ValueError(PySR.Runtime_Bytes_NegativeCount);
            if (count > long.MaxValue)
                return PyResult.OverflowError(PySR.Runtime_Bytes_IndexOverflow, source.PyType.Name);
            if (count > int.MaxValue)
                return PyResult.MemoryError(null);

            return PyByteArrayObject.FromBytes(new byte[(int)count]);
        }

        var iterResult = PySpecialMethods.Iter(context, source);
        if (iterResult.IsError)
        {
            // CPython replaces a failed GetIter with its own message
            if (PyTypeErrorObjectType.Shared.IsInstance(iterResult.Exception))
                return PyResult.TypeError(PySR.Runtime_ByteArray_CannotConvert, source.PyType.TpName);

            return iterResult;
        }

        var listResult = PyUtils.IteratorToList(context, iterResult.Value);
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
            var indicesResult = slice.Indices(context, self.Length, out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (start, _, step, length) = indices;
            return self.Slice(start, step, length);
        }

        var indexResult = PySpecialMethods.Index(context, item);
        if (indexResult.IsError)
            return indexResult;

        var index = PyUtils.MapIndex(indexResult.Value.Int32Value, self.Length);
        if (index < 0 || index >= self.Length)
            return PyResult.IndexError(PySR.Runtime_IndexOutOfRange);

        return PyIntObject.FromInteger(self[index]);
    }

    protected override PyResult SetItem(PyCallContext context, PyByteArrayObject self, PyObject key, PyObject value)
    {
        if (key is PySliceObject slice)
        {
            var indicesResult = slice.Indices(context, self.Length, out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (start, stop, step, sliceLength) = indices;
            var valuesResult = TryGetByteList(context, value, out var values);
            if (valuesResult.IsError)
                return valuesResult;

            if (step is not 1 && values.Count != sliceLength)
                return PyResult.ValueError(PySR.Runtime_Sequence_SliceStep_AssignWrongSize, sliceLength, values.Count);

            if (step is 1)
                self.ReplaceSliceStep1(start, stop, values);
            else
                self.ReplaceSliceStepN(start, step, sliceLength, values);

            return PyNoneObject.None;
        }

        var indexResult = PySpecialMethods.Index(context, key);
        if (indexResult.IsError)
            return indexResult;

        var mappedIndex = PyUtils.MapIndex(indexResult.Value.Int32Value, self.Length);
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

    // CPython's reflected wrappers cover as_number and sq_repeat slots
    // only; sq_concat has no reflected variant (bytearray.__radd__ does
    // not exist). __mul__ keeps its __rmul__ entry with the non-swapping
    // self*value order of wrap_indexargfunc.
    protected override bool SynthesizeReflectedAdd => false;
    protected override bool ReflectedMulSwapsOperands => false;

    protected override PyResult Add(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        if (!TryGetSpan(other, out var otherSpan))
        {
            // The right operand's reflected __radd__ runs before the
            // concat TypeError (CPython's bytearray has no nb_add).
            if (other.PyType.Slots.RAdd is not null)
            {
                var reflected = other.PyType.Slots.RAdd(context, other, self);
                if (!reflected.IsNotImplemented)
                    return reflected;
            }
            return PyResult.TypeError("can't concat {0} to bytearray", other.PyType.TpName);
        }

        var result = new byte[self.Length + otherSpan.Length];
        self.AsSpan().CopyTo(result);
        otherSpan.CopyTo(result.AsSpan()[self.Length..]);
        return PyByteArrayObject.FromBytes(result);
    }

    protected override PyResult IAdd(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        if (!TryGetSpan(other, out var otherSpan))
            return PyResult.TypeError("can't concat {0} to bytearray", other.PyType.TpName);

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

    // bytearray_richcompare accepts any bytes-like operand (bytes,
    // bytearray, memoryview) for order comparisons; bytes sends its
    // cross-type comparisons here through the reflected slot because
    // bytes_richcompare requires both operands to be exact bytes.
    protected override PyResult Lt(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        if (!TryGetSpan(other, out var otherSpan))
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(CompareContent(self.AsSpan(), otherSpan) < 0);
    }

    protected override PyResult Le(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        if (!TryGetSpan(other, out var otherSpan))
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(CompareContent(self.AsSpan(), otherSpan) <= 0);
    }

    protected override PyResult Gt(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        if (!TryGetSpan(other, out var otherSpan))
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(CompareContent(self.AsSpan(), otherSpan) > 0);
    }

    protected override PyResult Ge(PyCallContext context, PyByteArrayObject self, PyObject other)
    {
        if (!TryGetSpan(other, out var otherSpan))
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(CompareContent(self.AsSpan(), otherSpan) >= 0);
    }

    // memcmp over the content; SequenceCompareTo already breaks a common
    // prefix by length, matching Py_RETURN_RICHCOMPARE(len_a, len_b, op)
    private static int CompareContent(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        => left.SequenceCompareTo(right);

    protected override PyResult Hash(PyCallContext context, PyByteArrayObject self)
    {
        return PyResult.TypeError(PySR.Runtime_Object_Unhashable, self.PyType.TpName);
    }

    // bytearray_contains mirrors bytes_contains: a bytes-like operand is
    // searched as a subsequence, an index operand is byte membership with
    // the legacy 0..255 validation
    protected override PyResult Contains(PyCallContext context, PyByteArrayObject self, PyObject item)
    {
        if (TryGetSpan(item, out var sub))
            return PyBoolObject.FromBoolean(self.AsSpan().IndexOf(sub) >= 0);

        if (item is PyIntObject || item.PyType.Slots.Index is not null)
        {
            var indexResult = PySpecialMethods.Index(context, item);
            if (indexResult.IsError)
                return indexResult;

            var value = indexResult.Value.Value;
            if (value < 0 || value > 255)
                return PyResult.ValueError(PySR.Runtime_Bytes_ByteOutOfRange);
            return PyBoolObject.FromBoolean(self.AsSpan().Contains((byte)value));
        }

        return PyResult.TypeError(PySR.Runtime_Bytes_BytesLikeRequired, item.PyType.TpName);
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
        => PyBytesObjectType.TryGetBytesLikeSpan(source, out span);

    private static PyResult TryGetByteValue(PyCallContext context, PyObject item, out byte value)
    {
        value = default;
        var indexResult = PySpecialMethods.Index(context, item);
        if (indexResult.IsError)
            return indexResult;

        var intValue = indexResult.Value.Value;
        if (intValue < byte.MinValue || intValue > byte.MaxValue)
            return PyResult.ValueError(PySR.Runtime_Bytes_ByteOutOfRange);

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

        // CPython bytearray_extend's iterator path: the length hint is
        // consulted after starting iteration and its errors propagate (the
        // buffer fast path above never sees the hint)
        var iterator = PySpecialMethods.Iter(context, iterable);
        if (iterator.IsError)
            return iterator;

        var hintResult = PyUtils.LengthHint(context, iterable, 32);
        if (hintResult.IsError)
            return hintResult;
        if (hintResult.Value.Value > PyUtils.MaxPreallocationHint)
            return PyResult.MemoryError(null);

        var listResult = PyUtils.IteratorToList(context, iterator.Value);
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

    internal static string FormatBytesLiteral(ReadOnlySpan<byte> span)
    {
        var containsSingle = span.Contains((byte)'\'');
        var containsDouble = span.Contains((byte)'\"');
        var wrapper = containsSingle && !containsDouble ? '"' : '\'';

        var builder = new StringBuilder("b").Append(wrapper);
        foreach (byte b in span)
        {
            if (b == wrapper)
                builder.Append('\\').Append(wrapper);
            else if (b is (byte)'\\')
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
                builder.AppendFormat(CultureInfo.InvariantCulture, "\\x{0:x2}", b);
        }

        builder.Append(wrapper);
        return builder.ToString();
    }
}

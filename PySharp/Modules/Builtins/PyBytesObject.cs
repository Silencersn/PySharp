using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Text;

namespace PySharp.Modules.Builtins;

public sealed class PyBytesObject : PyObject
{
    private readonly byte[] _data;

    public byte this[int index] => _data[index];
    public int Length => _data.Length;
    public ReadOnlySpan<byte> AsSpan() => _data;

    public override PyTypeObject DefaultPyType => PyBytesObjectType.Shared;
    internal override bool IsImmutable => true;

    private PyBytesObject(byte[] data)
    {
        _data = data;
    }

    public static PyBytesObject Empty { get; } = new PyBytesObject([]);

    public static PyBytesObject FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return Empty;
        return new PyBytesObject(data.ToArray());
    }

    public static PyBytesObject FromBytes(byte[] data)
    {
        if (data.Length is 0)
            return Empty;

        return new PyBytesObject([.. data]);
    }

    internal static PyBytesObject MoveBytes(byte[] data)
    {
        return new PyBytesObject(data);
    }
}

[PyType("bytes")]
public sealed partial class PyBytesObjectType : PyTypeObject<PyBytesObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("source=b''")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var source = arguments[0];
        if (source is PyBytesObject)
            return source;

        if (source is PyStrObject)
            return PyResult.TypeError(PySR.Runtime_Bytes_StrWithoutEncoding);

        // CPython bytes(n): an indexable source zero-fills n bytes
        // (PyNumber_Index); the iterable protocol is only the fallback
        if (source.PyType.Slots.Index is not null)
        {
            var indexResult = PySpecialMethods.Index(context, source);
            if (indexResult.IsError)
            {
                // CPython replaces a failed top-level conversion with its
                // own message; item-level errors later on are untouched
                if (PyTypeErrorObjectType.Shared.IsInstance(indexResult.Exception))
                    return PyResult.TypeError(PySR.Runtime_Bytes_CannotConvert, source.PyType.FullName);

                return indexResult;
            }

            var count = indexResult.Value.Value;
            if (count < 0)
                return PyResult.ValueError(PySR.Runtime_Bytes_NegativeCount);
            if (count > long.MaxValue)
                return PyResult.OverflowError(PySR.Runtime_Bytes_IndexOverflow, source.PyType.Name);
            if (count > int.MaxValue)
                return PyResult.MemoryError(null);

            return PyBytesObject.MoveBytes(new byte[(int)count]);
        }

        var iterResult = PySpecialMethods.Iter(context, source);
        if (iterResult.IsError)
        {
            // CPython replaces a failed GetIter with its own message
            if (PyTypeErrorObjectType.Shared.IsInstance(iterResult.Exception))
                return PyResult.TypeError(PySR.Runtime_Bytes_CannotConvert, source.PyType.FullName);

            return iterResult;
        }

        var listResult = PyUtils.IteratorToList(context, iterResult.Value);
        if (listResult.IsError)
            return listResult;

        var bytes = new byte[listResult.Value.Count];
        for (int i = 0; i < listResult.Value.Count; i++)
        {
            PyObject? item = listResult.Value[i];
            var indexResult = PySpecialMethods.Index(context, item);
            if (indexResult.IsError)
                return indexResult;

            var value = indexResult.Value.Value;
            if (value < byte.MinValue || value > byte.MaxValue)
                return PyResult.ValueError(PySR.Runtime_Bytes_OutOfRange);

            bytes[i] = (byte)value;
        }
        return PyBytesObject.MoveBytes(bytes);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;

        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Repr(PyCallContext context, PyBytesObject self)
    {
        var span = self.AsSpan();
        var containsSingle = span.Contains((byte)'\'');
        var containsDouble = span.Contains((byte)'"');
        var wrapper = containsSingle && !containsDouble ? '"' : '\'';

        var builder = new StringBuilder("b").Append(wrapper);
        foreach (byte b in self.AsSpan())
        {
            if (b is (byte)'\'' && wrapper is '\'')
                builder.Append("\\'");
            else if (b is (byte)'"' && wrapper is '"')
                builder.Append("\\\"");
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
                builder.AppendFormat("\\x{0:x2}", b);
        }
        builder.Append(wrapper);
        return PyStrObject.FromString(builder.ToString());
    }

    protected override PyResult Len(PyCallContext context, PyBytesObject self)
    {
        return PyIntObject.FromInteger(self.Length);
    }

    protected override PyResult GetItem(PyCallContext context, PyBytesObject self, PyObject item)
    {
        if (item is PySliceObject slice)
        {
            var indicesResult = slice.Indices(context, self.Length, out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (start, _, step, length) = indices;
            if (length is 0)
                return PyBytesObject.Empty;

            var result = new byte[length];
            for (int i = 0, j = start; i < length; i++, j += step)
                result[i] = self[j];

            return PyBytesObject.MoveBytes(result);
        }

        var indexResult = PySpecialMethods.Index(context, item);
        if (indexResult.IsError)
            return indexResult;

        var index = PyUtils.MapIndex(indexResult.Value.Int32Value, self.Length);
        if (index < 0 || index >= self.Length)
            return PyResult.IndexError(PySR.Runtime_IndexOutOfRange);

        return PyIntObject.FromInteger(self[index]);
    }

    protected override PyResult Iter(PyCallContext context, PyBytesObject self)
    {
        return new PyBytesIteratorObject(self);
    }

    protected override PyResult Add(PyCallContext context, PyBytesObject self, PyObject other)
    {
        // bytes_concat accepts any bytes-like operand; the concat TypeError
        // is reserved for fully unrelated types
        if (!TryGetBytesLikeSpan(other, out var otherSpan))
            return PyResult.TypeError(PySR.Runtime_Bytes_CannotConcat, other.PyType.FullName);

        var combinedBytes = new byte[self.Length + otherSpan.Length];
        var dstSpan = combinedBytes.AsSpan();
        self.AsSpan().CopyTo(dstSpan);
        otherSpan.CopyTo(dstSpan[self.Length..]);
        return PyBytesObject.MoveBytes(combinedBytes);
    }

    protected override PyResult Mul(PyCallContext context, PyBytesObject self, PyObject other)
    {
        var indexResult = PySpecialMethods.Index(context, other);
        if (indexResult.IsError)
            return indexResult;

        var n = indexResult.Value.Value;
        if (n <= 0)
            return PyBytesObject.Empty;

        if (n == 1)
            return self;

        var intN = (int)n;
        var result = new byte[self.Length * intN];
        var srcSpan = self.AsSpan();
        var dstSpan = result.AsSpan();
        for (int i = 0; i < intN; i++)
            srcSpan.CopyTo(dstSpan[(i * srcSpan.Length)..]);
        return PyBytesObject.FromBytes(result);
    }

    protected override PyResult RMul(PyCallContext context, PyBytesObject self, PyObject other)
    {
        return Mul(context, self, other);
    }

    protected override PyResult Eq(PyCallContext context, PyBytesObject self, PyObject other)
    {
        if (other is not PyBytesObject otherBytes)
            return PyNotImplementedObject.NotImplemented;

        return PyBoolObject.FromBoolean(self.AsSpan().SequenceEqual(otherBytes.AsSpan()));
    }

    protected override PyResult Hash(PyCallContext context, PyBytesObject self)
    {
        unchecked
        {
            int hash = self.Length;
            foreach (byte b in self.AsSpan())
                hash = hash * 31 + b;
            return PyIntObject.FromInteger(hash);
        }
    }

    protected override PyResult Lt(PyCallContext context, PyBytesObject self, PyObject other)
    {
        if (other is not PyBytesObject otherBytes)
            return PyNotImplementedObject.NotImplemented;

        var minLen = Math.Min(self.Length, otherBytes.Length);
        for (int i = 0; i < minLen; i++)
        {
            var l = self[i];
            var r = otherBytes[i];

            if (l < r)
                return PyBoolObject.True;
            else if (l > r)
                return PyBoolObject.False;
        }

        return PyBoolObject.FromBoolean(self.Length < otherBytes.Length);
    }

    // Buffer-protocol types accepted wherever CPython takes a bytes-like
    // operand: bytes, bytearray, memoryview
    internal static bool TryGetBytesLikeSpan(PyObject source, out ReadOnlySpan<byte> span)
    {
        switch (source)
        {
            case PyBytesObject bytes:
                span = bytes.AsSpan();
                return true;
            case PyByteArrayObject byteArray:
                span = byteArray.AsSpan();
                return true;
            case PyMemoryViewObject view:
                span = view.DataSpan;
                return true;
            default:
                span = default;
                return false;
        }
    }

    protected override PyResult Contains(PyCallContext context, PyBytesObject self, PyObject item)
    {
        // bytes_contains: a bytes-like operand is searched as a subsequence;
        // an index operand is byte membership with the legacy 0..255
        // validation; anything else is rejected with the buffer error
        if (TryGetBytesLikeSpan(item, out var sub))
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

        return PyResult.TypeError(PySR.Runtime_Bytes_BytesLikeRequired, item.PyType.FullName);
    }

    [PyMethod("decode")]
    [AIGenerated]
    [PyFunctionParameters("encoding='utf-8'", "/")]
    private static PyResult Decode(PyCallContext context, PyBytesObject self, PyArguments arguments)
    {
        string encoding = "utf-8";
        if (arguments[0] is PyStrObject encStr)
            encoding = encStr.Value;
        else if (arguments[0] is not PyNoneObject)
            return PyResult.TypeError("decoding must be str");

        try
        {
            var enc = PyStrObjectType.GetEncoding(encoding);
            var data = self.AsSpan();

            // CPython's bare utf-16/utf-32 codecs treat a leading BOM as the
            // byte order mark: it selects the decode order and is dropped;
            // with no BOM the native (little-endian) order applies. The
            // explicit -le/-be variants leave a BOM in the decoded text.
            int bomLength = 0;
            bool bigEndian = false;
            switch (PyStrObjectType.NormalizeEncodingName(encoding))
            {
                case "utf16":
                    if (data.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
                        (bomLength, bigEndian) = (2, true);
                    else if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
                        bomLength = 2;
                    if (bigEndian)
                        enc = Encoding.BigEndianUnicode;
                    break;
                case "utf32":
                    if (data.StartsWith((ReadOnlySpan<byte>)[0x00, 0x00, 0xFE, 0xFF]))
                        (bomLength, bigEndian) = (4, true);
                    else if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE, 0x00, 0x00]))
                        bomLength = 4;
                    if (bigEndian)
                        enc = new UTF32Encoding(bigEndian: true, byteOrderMark: false);
                    break;
            }

            string result = enc.GetString(bomLength > 0 ? data[bomLength..] : data);
            return PyStrObject.FromString(result);
        }
        catch (ArgumentException)
        {
            return PyResult.ValueError($"unknown encoding: {encoding}");
        }
    }
}
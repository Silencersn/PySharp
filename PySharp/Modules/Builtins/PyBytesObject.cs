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
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython bytes_new: the clinic parser reports arity, keyword and
        // str-converter failures before bytes_new_impl sees the arguments;
        // a string source encodes, and non-string sources then take the
        // zero-fill / iterable / copy conversions
        var bindResult = BindCodecArguments("bytes", args, kwargs, out var source, out var encoding, out var errors);
        if (bindResult.IsError)
            return bindResult;

        var encodingResult = CheckCodecStrArgument("bytes", "encoding", encoding, out var encodingName);
        if (encodingResult.IsError)
            return encodingResult;

        var errorsResult = CheckCodecStrArgument("bytes", "errors", errors, out var errorsName);
        if (errorsResult.IsError)
            return errorsResult;

        if (source is PyStrObject strSource)
        {
            if (encodingName is null)
                return PyResult.TypeError(PySR.Runtime_Bytes_StrWithoutEncoding);
            return PyStrObjectType.EncodeCore(context, strSource.Value, encodingName, errorsName ?? "strict");
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
            return PyBytesObject.Empty;

        if (source is PyBytesObject)
            return source;

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

    // Clinic binding shared by the bytes()/bytearray() (source, encoding,
    // errors) signatures: arity above three, unknown keyword names and
    // keyword/positional collisions report before any conversion runs
    internal static PyResult BindCodecArguments(string typeName, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs, out PyObject? source, out PyObject? encoding, out PyObject? errors)
    {
        source = args.Count > 0 ? args[0] : null;
        encoding = args.Count > 1 ? args[1] : null;
        errors = args.Count > 2 ? args[2] : null;

        if (args.Count + kwargs.Count > 3)
            return PyResult.TypeError(PySR.Runtime_Codec_TakesAtMostThreeArgs, typeName, args.Count + kwargs.Count);

        foreach (var (name, value) in kwargs)
        {
            switch (name)
            {
                case "source" when source is null:
                    source = value;
                    break;
                case "encoding" when encoding is null:
                    encoding = value;
                    break;
                case "errors" when errors is null:
                    errors = value;
                    break;
                case "source" or "encoding" or "errors":
                    return PyResult.TypeError(PySR.Runtime_Codec_MultipleValues, typeName, name, name is "source" ? 1 : name is "encoding" ? 2 : 3);
                default:
                    return PyResult.TypeError(PySR.Runtime_Codec_UnexpectedKeyword, typeName, name);
            }
        }

        return PyNoneObject.None;
    }

    // Clinic 'str' converters reject non-str before the guards; None
    // reports as "None", other types by name
    internal static PyResult CheckCodecStrArgument(string typeName, string name, PyObject? value, out string? text)
    {
        if (value is null)
        {
            text = null;
            return PyNoneObject.None;
        }
        if (value is PyStrObject str)
        {
            text = str.Value;
            return PyNoneObject.None;
        }
        text = null;
        return PyResult.TypeError(PySR.Runtime_Codec_ArgMustBeStr, typeName, name, value is PyNoneObject ? "None" : value.PyType.Name);
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
        {
        // The reflected __radd__ of the right operand gets the first chance
        // (CPython's bytes has no nb_add); the concat TypeError is the last
        // resort when it declines or does not exist.
        if (other.PyType.Slots.RAdd is not null)
        {
            var reflected = other.PyType.Slots.RAdd(context, other, self);
            if (!reflected.IsNotImplemented)
                return reflected;
        }
        return PyResult.TypeError(PySR.Runtime_Bytes_CannotConcat, other.PyType.FullName);
        }

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

    // bytes_richcompare: order comparisons require two exact bytes
    // operands — bytes-like types compare through bytearray's reflected
    // slot instead, mirroring CPython's do_richcompare fallback
    protected override PyResult Lt(PyCallContext context, PyBytesObject self, PyObject other)
    {
        if (other is not PyBytesObject otherBytes)
            return PyNotImplementedObject.NotImplemented;

        return PyBoolObject.FromBoolean(CompareContent(self.AsSpan(), otherBytes.AsSpan()) < 0);
    }

    protected override PyResult Le(PyCallContext context, PyBytesObject self, PyObject other)
    {
        if (other is not PyBytesObject otherBytes)
            return PyNotImplementedObject.NotImplemented;

        return PyBoolObject.FromBoolean(CompareContent(self.AsSpan(), otherBytes.AsSpan()) <= 0);
    }

    protected override PyResult Gt(PyCallContext context, PyBytesObject self, PyObject other)
    {
        if (other is not PyBytesObject otherBytes)
            return PyNotImplementedObject.NotImplemented;

        return PyBoolObject.FromBoolean(CompareContent(self.AsSpan(), otherBytes.AsSpan()) > 0);
    }

    protected override PyResult Ge(PyCallContext context, PyBytesObject self, PyObject other)
    {
        if (other is not PyBytesObject otherBytes)
            return PyNotImplementedObject.NotImplemented;

        return PyBoolObject.FromBoolean(CompareContent(self.AsSpan(), otherBytes.AsSpan()) >= 0);
    }

    // memcmp over the content; SequenceCompareTo already breaks a common
    // prefix by length, matching Py_RETURN_RICHCOMPARE(len_a, len_b, op)
    private static int CompareContent(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        => left.SequenceCompareTo(right);

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
    [PyFunctionParameters("encoding='utf-8'", "errors='strict'")]
    private static PyResult Decode(PyCallContext context, PyBytesObject self, PyArguments arguments)
    {
        string encoding = "utf-8";
        if (arguments[0] is PyStrObject encStr)
            encoding = encStr.Value;
        else if (arguments[0] is not PyNoneObject)
            return PyResult.TypeError("decoding must be str");

        string errors = "strict";
        if (arguments[1] is PyStrObject errStr)
            errors = errStr.Value;
        else if (arguments[1] is not PyNoneObject)
            return PyResult.TypeError("errors must be str");

        return DecodeCore(context, self.AsSpan(), encoding, errors, self);
    }

    // The bytes.decode core shared with the str(bytes, encoding) constructor
    // form: codec resolution and the bare utf-16/32 BOM handling all match
    // PyUnicode_Decode. The core codecs (utf-8, ascii, latin-1, utf-16,
    // utf-32) decode bytewise with CPython's exact error reasons and
    // subparts; errors= is resolved lazily so valid input never rejects an
    // unknown handler name, and strict raises UnicodeDecodeError carrying
    // encoding/object/start/end/reason.
    internal static PyResult DecodeCore(PyCallContext context, ReadOnlySpan<byte> data, string encoding, string errors = "strict", PyObject? source = null)
    {
        Encoding enc;
        try
        {
            enc = PyStrObjectType.GetEncoding(encoding);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return PyResult.LookupError(PySR.Runtime_Codec_UnknownEncoding, encoding);
        }

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
                break;
            case "utf32":
                if (data.StartsWith((ReadOnlySpan<byte>)[0x00, 0x00, 0xFE, 0xFF]))
                    (bomLength, bigEndian) = (4, true);
                else if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE, 0x00, 0x00]))
                    bomLength = 4;
                break;
        }

        var payload = bomLength > 0 ? data[bomLength..] : data;
        var sourceObject = source ?? PyBytesObject.FromBytes(payload);

        switch (PyStrObjectType.NormalizeEncodingName(encoding))
        {
            case "utf8":
                return DecodeUtf8(payload, "utf-8", errors, sourceObject);
            case "ascii" or "usascii" or "us" or "646" or "iso646us" or "ansix341968" or "ansix341986" or "isoir6" or "csascii" or "ibm367" or "cp367":
                return DecodeSingleByte(payload, "ascii", errors, sourceObject, ordinalMax: 0x7F);
            // latin-1 (under any of its aliases) maps every byte, it can
            // never fail
            case "latin1" or "latin" or "l1" or "8859" or "88591" or "iso8859" or "iso88591" or "iso885911987" or "isoir100" or "csisolatin1" or "ibm819" or "cp819":
            {
                var latin = new StringBuilder(payload.Length);
                foreach (var b in payload)
                    latin.Append((char)b);
                return PyStrObject.FromString(latin.ToString());
            }
            case "utf16":
                return DecodeUtf16(payload, bigEndian, "utf-16", errors, sourceObject);
            case "utf16le":
                return DecodeUtf16(payload, bigEndian: false, "utf-16-le", errors, sourceObject);
            case "utf16be":
                return DecodeUtf16(payload, bigEndian: true, "utf-16-be", errors, sourceObject);
            case "utf32":
                return DecodeUtf32(payload, bigEndian, "utf-32", errors, sourceObject);
            case "utf32le":
                return DecodeUtf32(payload, bigEndian: false, "utf-32-le", errors, sourceObject);
            case "utf32be":
                return DecodeUtf32(payload, bigEndian: true, "utf-32-be", errors, sourceObject);
            default:
                return DecodeViaDotNet(payload, enc, encoding, errors, sourceObject);
        }
    }

    internal enum DecodeErrorHandler
    {
        Strict,
        Ignore,
        Replace,
        BackslashReplace,
        SurrogateEscape,
        SurrogatePass,
    }

    private static bool TryResolveErrors(string errors, out DecodeErrorHandler handler)
    {
        switch (errors)
        {
            case "strict": handler = DecodeErrorHandler.Strict; return true;
            case "ignore": handler = DecodeErrorHandler.Ignore; return true;
            case "replace": handler = DecodeErrorHandler.Replace; return true;
            case "backslashreplace": handler = DecodeErrorHandler.BackslashReplace; return true;
            case "surrogateescape": handler = DecodeErrorHandler.SurrogateEscape; return true;
            case "surrogatepass": handler = DecodeErrorHandler.SurrogatePass; return true;
            default: handler = default; return false;
        }
    }

    private static PyResult.PyExceptionResult UnknownErrorHandler(string errors)
    {
        return PyResult.LookupError($"unknown error handler name '{errors}'");
    }

    private static PyResult.PyExceptionResult UnicodeDecodeError(string codecName, PyObject source, ReadOnlySpan<byte> data, int start, int end, string reason)
    {
        var exc = PyExceptionObject.UnsafeCreate(PyUnicodeDecodeErrorObjectType.Shared,
            [PyStrObject.FromString(codecName), source, PyIntObject.FromInteger(start), PyIntObject.FromInteger(end), PyStrObject.FromString(reason)]);
        return new(exc);
    }

    // one STRINGLIB(utf8_decode) iteration: decodes the sequence starting at
    // data[i], appending to sb; returns 0 on success with i advanced, -1 for
    // a sequence truncated at the end of input, or the CPython error code
    // 1..4 leaving i at the sequence start
    private static int Utf8DecodeStep(ReadOnlySpan<byte> data, ref int i, StringBuilder sb)
    {
        int s = i;
        int ch = data[s];

        if (ch < 0x80)
        {
            sb.Append((char)ch);
            i = s + 1;
            return 0;
        }

        if (ch < 0xE0)
        {
            if (ch < 0xC2)
                return 1;
            if (data.Length - s < 2)
                return -1;
            int ch2 = data[s + 1];
            if (!IsContinuationByte(ch2))
                return 2;
            sb.Append((char)(((ch & 0x1F) << 6) | (ch2 & 0x3F)));
            i = s + 2;
            return 0;
        }

        if (ch < 0xF0)
        {
            if (data.Length - s < 3)
            {
                if (data.Length - s < 2)
                    return -1;
                int shortCh2 = data[s + 1];
                if (!IsContinuationByte(shortCh2) || (shortCh2 < 0xA0 ? ch is 0xE0 : ch is 0xED))
                    return 2;
                return -1;
            }
            int ch2 = data[s + 1];
            int ch3 = data[s + 2];
            if (!IsContinuationByte(ch2))
                return 2;
            if (ch is 0xE0 && ch2 < 0xA0)
                return 2;
            if (ch is 0xED && ch2 >= 0xA0)
                return 2;
            if (!IsContinuationByte(ch3))
                return 3;
            sb.Append((char)(((ch & 0x0F) << 12) | ((ch2 & 0x3F) << 6) | (ch3 & 0x3F)));
            i = s + 3;
            return 0;
        }

        if (ch < 0xF5)
        {
            if (data.Length - s < 4)
            {
                if (data.Length - s < 2)
                    return -1;
                int shortCh2 = data[s + 1];
                if (!IsContinuationByte(shortCh2) || (shortCh2 < 0x90 ? ch is 0xF0 : ch is 0xF4))
                    return 2;
                if (data.Length - s < 3)
                    return -1;
                if (!IsContinuationByte(data[s + 2]))
                    return 3;
                return -1;
            }
            int ch2 = data[s + 1];
            int ch3 = data[s + 2];
            int ch4 = data[s + 3];
            if (!IsContinuationByte(ch2))
                return 2;
            if (ch is 0xF0 && ch2 < 0x90)
                return 2;
            if (ch is 0xF4 && ch2 >= 0x90)
                return 2;
            if (!IsContinuationByte(ch3))
                return 3;
            if (!IsContinuationByte(ch4))
                return 4;
            int c = ((ch & 0x07) << 18) | ((ch2 & 0x3F) << 12) | ((ch3 & 0x3F) << 6) | (ch4 & 0x3F);
            c -= 0x10000;
            sb.Append((char)(0xD800 + (c >> 10)));
            sb.Append((char)(0xDC00 + (c & 0x3FF)));
            i = s + 4;
            return 0;
        }

        return 1;
    }

    private static bool IsContinuationByte(int b)
    {
        return b >= 0x80 && b < 0xC0;
    }

    private static PyResult DecodeUtf8(ReadOnlySpan<byte> data, string codecName, string errors, PyObject source)
    {
        var sb = new StringBuilder(data.Length);
        int i = 0;
        while (i < data.Length)
        {
            int start = i;
            int code = Utf8DecodeStep(data, ref i, sb);
            if (code is 0)
                continue;

            int end;
            string reason;
            if (code < 0)
            {
                // a truncated sequence swallows the whole remaining tail
                end = data.Length;
                reason = "unexpected end of data";
            }
            else
            {
                end = start + (code is 1 ? 1 : code - 1);
                reason = code is 1 ? "invalid start byte" : "invalid continuation byte";
            }

            if (!TryResolveErrors(errors, out var handler))
                return UnknownErrorHandler(errors);

            // CPython surrogatepass: an encoded surrogate sequence
            // (ED A0..BF 80..BF) decodes to its surrogate; other errors
            // stay strict
            if (handler is DecodeErrorHandler.SurrogatePass)
            {
                if (data[start] is 0xED && start + 2 < data.Length &&
                    data[start + 1] is >= 0xA0 and <= 0xBF && IsContinuationByte(data[start + 2]))
                {
                    sb.Append((char)(0xD800 + ((data[start + 1] & 0x0F) << 6) + (data[start + 2] & 0x3F)));
                    i = start + 3;
                    continue;
                }
                return UnicodeDecodeError(codecName, source, data, start, end, reason);
            }

            switch (handler)
            {
                case DecodeErrorHandler.Ignore:
                    break;
                case DecodeErrorHandler.Replace:
                    sb.Append('\uFFFD');
                    break;
                case DecodeErrorHandler.BackslashReplace:
                case DecodeErrorHandler.SurrogateEscape:
                    AppendBytesAsEscapes(sb, data[start..end], handler);
                    break;
                default:
                    return UnicodeDecodeError(codecName, source, data, start, end, reason);
            }
            i = end;
        }
        return PyStrObject.FromString(sb.ToString());
    }

    private static void AppendBytesAsEscapes(StringBuilder sb, ReadOnlySpan<byte> bytes, DecodeErrorHandler handler)
    {
        foreach (var b in bytes)
        {
            switch (handler)
            {
                case DecodeErrorHandler.BackslashReplace:
                    sb.Append("\\x").Append(b.ToString("x2"));
                    break;
                case DecodeErrorHandler.SurrogateEscape:
                    sb.Append((char)(0xDC00 + b));
                    break;
            }
        }
    }

    private static PyResult DecodeSingleByte(ReadOnlySpan<byte> data, string codecName, string errors, PyObject source, int ordinalMax)
    {
        var sb = new StringBuilder(data.Length);
        int i = 0;
        while (i < data.Length)
        {
            int b = data[i];
            if (b <= ordinalMax)
            {
                sb.Append((char)b);
                i++;
                continue;
            }

            if (!TryResolveErrors(errors, out var handler))
                return UnknownErrorHandler(errors);
            switch (handler)
            {
                case DecodeErrorHandler.Ignore:
                    break;
                case DecodeErrorHandler.Replace:
                    sb.Append('\uFFFD');
                    break;
                case DecodeErrorHandler.BackslashReplace:
                    sb.Append("\\x").Append(b.ToString("x2"));
                    break;
                case DecodeErrorHandler.SurrogateEscape:
                    sb.Append((char)(0xDC00 + b));
                    break;
                case DecodeErrorHandler.SurrogatePass:
                default:
                    return UnicodeDecodeError(codecName, source, data, i, i + 1, "ordinal not in range(128)");
            }
            i++;
        }
        return PyStrObject.FromString(sb.ToString());
    }

    private static PyResult DecodeUtf16(ReadOnlySpan<byte> data, bool bigEndian, string codecName, string errors, PyObject source)
    {
        var sb = new StringBuilder(data.Length / 2);
        int i = 0;
        while (i + 1 < data.Length)
        {
            int unit = bigEndian ? (data[i] << 8) | data[i + 1] : data[i] | (data[i + 1] << 8);

            int start;
            int end;
            string reason;
            if (unit is >= 0xDC00 and <= 0xDFFF)
            {
                // a lone low surrogate: the pair cannot combine
                start = i;
                end = i + 2;
                reason = "illegal encoding";
            }
            else if (unit is >= 0xD800 and <= 0xDBFF)
            {
                if (i + 3 >= data.Length)
                {
                    // the high surrogate's tail is the final byte
                    if (i + 2 == data.Length - 1)
                    {
                        start = i;
                        end = data.Length;
                        reason = "unexpected end of data";
                    }
                    else
                    {
                        start = i;
                        end = i + 2;
                        reason = "unexpected end of data";
                    }
                }
                else
                {
                    int low = bigEndian ? (data[i + 2] << 8) | data[i + 3] : data[i + 2] | (data[i + 3] << 8);
                    if (low is >= 0xDC00 and <= 0xDFFF)
                    {
                        int c = 0x10000 + ((unit - 0xD800) << 10) + (low - 0xDC00);
                        sb.Append((char)(0xD800 + ((c - 0x10000) >> 10)));
                        sb.Append((char)(0xDC00 + ((c - 0x10000) & 0x3FF)));
                        i += 4;
                        continue;
                    }
                    start = i;
                    end = i + 2;
                    reason = "illegal UTF-16 surrogate";
                }
            }
            else
            {
                sb.Append((char)unit);
                i += 2;
                continue;
            }

            if (!TryResolveErrors(errors, out var handler))
                return UnknownErrorHandler(errors);

            // CPython surrogatepass: a complete lone surrogate unit passes
            // through as its own code point
            if (handler is DecodeErrorHandler.SurrogatePass && end - start is 2)
            {
                sb.Append((char)unit);
                i = end;
                continue;
            }

            switch (handler)
            {
                case DecodeErrorHandler.Ignore:
                    break;
                case DecodeErrorHandler.Replace:
                    sb.Append('\uFFFD');
                    break;
                case DecodeErrorHandler.BackslashReplace:
                case DecodeErrorHandler.SurrogateEscape:
                    AppendBytesAsEscapes(sb, data[start..end], handler);
                    break;
                default:
                    return UnicodeDecodeError(codecName, source, data, start, end, reason);
            }
            i = end;
        }

        if (data.Length % 2 is not 0)
        {
            int last = data.Length - 1;
            if (!TryResolveErrors(errors, out var handler))
                return UnknownErrorHandler(errors);
            switch (handler)
            {
                case DecodeErrorHandler.Ignore:
                    break;
                case DecodeErrorHandler.Replace:
                    sb.Append('\uFFFD');
                    break;
                case DecodeErrorHandler.BackslashReplace:
                case DecodeErrorHandler.SurrogatePass:
                case DecodeErrorHandler.SurrogateEscape:
                    AppendBytesAsEscapes(sb, data[last..], handler);
                    break;
                default:
                    return UnicodeDecodeError(codecName, source, data, last, data.Length, "truncated data");
            }
        }
        return PyStrObject.FromString(sb.ToString());
    }

    private static PyResult DecodeUtf32(ReadOnlySpan<byte> data, bool bigEndian, string codecName, string errors, PyObject source)
    {
        var sb = new StringBuilder(data.Length / 4);
        int i = 0;
        while (i + 3 < data.Length)
        {
            uint unit = bigEndian
                ? (uint)(data[i] << 24 | data[i + 1] << 16 | data[i + 2] << 8 | data[i + 3])
                : (uint)(data[i] | data[i + 1] << 8 | data[i + 2] << 16 | data[i + 3] << 24);

            if (unit is > 0x10FFFF or >= 0xD800 and <= 0xDFFF)
            {
                if (!TryResolveErrors(errors, out var handler))
                    return UnknownErrorHandler(errors);

                // CPython surrogatepass: surrogate code points pass through
                if (handler is DecodeErrorHandler.SurrogatePass && unit is >= 0xD800 and <= 0xDFFF)
                {
                    sb.Append(char.ConvertFromUtf32((int)unit));
                    i += 4;
                    continue;
                }

                switch (handler)
                {
                    case DecodeErrorHandler.Ignore:
                        break;
                    case DecodeErrorHandler.Replace:
                        sb.Append('\uFFFD');
                        break;
                    case DecodeErrorHandler.BackslashReplace:
                    case DecodeErrorHandler.SurrogateEscape:
                        AppendBytesAsEscapes(sb, data[i..(i + 4)], handler);
                        break;
                    default:
                        return UnicodeDecodeError(codecName, source, data, i, i + 4, "illegal encoding");
                }
            }
            else
            {
                sb.Append(char.ConvertFromUtf32((int)unit));
            }
            i += 4;
        }

        if (data.Length % 4 is not 0)
        {
            int start = data.Length - data.Length % 4;
            if (!TryResolveErrors(errors, out var handler))
                return UnknownErrorHandler(errors);
            switch (handler)
            {
                case DecodeErrorHandler.Ignore:
                    break;
                case DecodeErrorHandler.Replace:
                    sb.Append('\uFFFD');
                    break;
                case DecodeErrorHandler.BackslashReplace:
                case DecodeErrorHandler.SurrogatePass:
                case DecodeErrorHandler.SurrogateEscape:
                    AppendBytesAsEscapes(sb, data[start..], handler);
                    break;
                default:
                    return UnicodeDecodeError(codecName, source, data, start, data.Length, "truncated data");
            }
        }
        return PyStrObject.FromString(sb.ToString());
    }

    // codecs without a hand-rolled core decode through .NET with the error
    // handler expressed as a decoder fallback; strict keeps the CPython
    // single-byte message shape
    private static PyResult DecodeViaDotNet(ReadOnlySpan<byte> data, Encoding enc, string encoding, string errors, PyObject source)
    {
        if (!TryResolveErrors(errors, out var handler))
            return UnknownErrorHandler(errors);

        DecoderFallback fallback = handler switch
        {
            DecodeErrorHandler.Ignore => new DecoderReplacementFallback(string.Empty),
            DecodeErrorHandler.Replace => new DecoderReplacementFallback("\uFFFD"),
            DecodeErrorHandler.BackslashReplace => new ByteRendererFallback(b => $"\\x{b:x2}"),
            DecodeErrorHandler.SurrogatePass or DecodeErrorHandler.SurrogateEscape => new ByteRendererFallback(b => char.ToString((char)(0xDC00 + b))),
            _ => new DecoderExceptionFallback(),
        };

        try
        {
            var strict = Encoding.GetEncoding(enc.CodePage, new EncoderExceptionFallback(), fallback);
            return PyStrObject.FromString(strict.GetString(data));
        }
        catch (DecoderFallbackException ex)
        {
            int start = ex.Index;
            int end = start + (ex.BytesUnknown is { Length: > 0 } ? ex.BytesUnknown.Length : 1);
            return UnicodeDecodeError(encoding, source, data, start, end, "invalid data");
        }
        catch (ArgumentException)
        {
            // this encoding cannot host fallback objects; degrade to its
            // default replacement behavior
            return PyStrObject.FromString(enc.GetString(data));
        }
    }

    private sealed class ByteRendererFallback(Func<byte, string> renderer) : DecoderFallback
    {
        public override int MaxCharCount => 8;

        public override DecoderFallbackBuffer CreateFallbackBuffer()
        {
            return new ByteRendererFallbackBuffer(renderer);
        }

        private sealed class ByteRendererFallbackBuffer(Func<byte, string> renderer) : DecoderFallbackBuffer
        {
            private string? _remaining;
            private int _index;

            public override bool Fallback(byte[] bytesUnknown, int index)
            {
                _remaining = renderer(bytesUnknown[0]);
                _index = 0;
                return true;
            }

            public override char GetNextChar()
            {
                if (_remaining is null || _index >= _remaining.Length)
                    return '\0';
                return _remaining[_index++];
            }

            public override int Remaining
            {
                get { return _remaining is not null && _index < _remaining.Length ? _remaining.Length - _index : 0; }
            }

            public override bool MovePrevious()
            {
                if (_index > 0)
                {
                    _index--;
                    return true;
                }
                return false;
            }

            public override void Reset()
            {
                _remaining = null;
                _index = 0;
            }
        }
    }
}
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
    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    [PyFunctionParameters("source=b''")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var source = arguments[0];
        if (source is PyBytesObject)
            return source;

        if (source is PyStrObject)
            return PyResult.TypeError(PySR.Runtime_Bytes_StrWithoutEncoding);

        var listResult = PyUtils.IterableToList(context, source);
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
            var (start, _, step, length) = slice.Indices(self.Length);
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

        var index = Utils.MapIndex(indexResult.Value.Int32Value, self.Length);
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
        if (other is not PyBytesObject otherBytes)
            return PyResult.TypeError(PySR.Runtime_Bytes_CannotConcat, other.PyType.FullName);

        var combinedBytes = new byte[self.Length + otherBytes.Length];
        var dstSpan = combinedBytes.AsSpan();
        self.AsSpan().CopyTo(dstSpan);
        otherBytes.AsSpan().CopyTo(dstSpan[self.Length..]);
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
}
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Text;

namespace PySharp.Modules.Builtins;

/// <summary>
/// Python file object returned by open().
/// Wraps a .NET Stream and provides text/binary file I/O.
/// </summary>
public sealed class PyFileObject : PyObject
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private readonly Stream _stream;
    private readonly bool _isTextMode;
    internal readonly bool _isReadable;
    internal readonly bool _isWritable;
    internal readonly bool _isSeekable;
    internal readonly string _mode;
    internal readonly string _name;
    private bool _closed;

    // Text mode helpers
    private StreamReader? _reader;
    private StreamWriter? _writer;

    internal PyFileObject(Stream stream, string mode, string name,
        bool isTextMode, bool isReadable, bool isWritable, bool isSeekable)
    {
        _stream = stream;
        _mode = mode;
        _name = name;
        _isTextMode = isTextMode;
        _isReadable = isReadable;
        _isWritable = isWritable;
        _isSeekable = isSeekable;

        if (isTextMode && isReadable)
            _reader = new StreamReader(stream, Utf8NoBom);
        if (isTextMode && isWritable)
            _writer = new StreamWriter(stream, Utf8NoBom);
    }

    public override PyTypeObject DefaultPyType => PyFileObjectType.Shared;
    internal bool IsClosed => _closed;

    internal PyResult CheckClosed()
    {
        if (_closed)
            return PyResult.ValueError(PySR.Runtime_File_Closed);
        return default;
    }

    internal PyResult Read(PyCallContext context, int size = -1)
    {
        var check = CheckClosed();
        if (check.IsError) return check;
        if (!_isReadable)
            return PyResult.ValueError(PySR.Runtime_File_NotReadable);

        if (_isTextMode)
        {
            Debug.Assert(_reader is not null);
            string result;
            if (size < 0)
                result = _reader.ReadToEnd();
            else if (size is 0)
                result = string.Empty;
            else
            {
                var buf = new char[size];
                var count = _reader.Read(buf, 0, size);
                result = new string(buf, 0, count);
            }
            return PyStrObject.FromString(result);
        }
        else
        {
            if (size < 0)
            {
                using var ms = new MemoryStream();
                _stream.CopyTo(ms);
                return PyBytesObject.FromBytes(ms.ToArray());
            }
            else if (size is 0)
            {
                return PyBytesObject.FromBytes([]);
            }
            else
            {
                var buf = new byte[size];
                var count = _stream.Read(buf, 0, size);
                if (count < size)
                    Array.Resize(ref buf, count);
                return PyBytesObject.FromBytes(buf);
            }
        }
    }

    internal PyResult Write(PyCallContext context, PyObject data)
    {
        var check = CheckClosed();
        if (check.IsError) return check;
        if (!_isWritable)
            return PyResult.ValueError(PySR.Runtime_File_NotWritable);

        if (_isTextMode)
        {
            Debug.Assert(_writer is not null);
            if (data is not PyStrObject strObj)
                return PyResult.TypeError(PySR.Runtime_File_WriteNeedStr, data.PyType.FullName);
            _writer.Write(strObj.Value);
            _writer.Flush();
            return PyIntObject.FromInteger(strObj.Value.Length);
        }
        else
        {
            if (data is not PyBytesObject bytesObj)
                return PyResult.TypeError(PySR.Runtime_File_WriteNeedBytes, data.PyType.FullName);
            var span = bytesObj.AsSpan();
            _stream.Write(span);
            return PyIntObject.FromInteger(span.Length);
        }
    }

    internal PyResult Close()
    {
        if (_closed)
            return PyNoneObject.None;
        _closed = true;
        _reader?.Dispose();
        _reader = null;
        _writer?.Dispose();
        _writer = null;
        _stream.Dispose();
        return PyNoneObject.None;
    }

    internal PyResult Flush()
    {
        var check = CheckClosed();
        if (check.IsError) return check;
        _writer?.Flush();
        _stream.Flush();
        return PyNoneObject.None;
    }

    internal PyResult Seek(long offset, int whence = 0)
    {
        var check = CheckClosed();
        if (check.IsError) return check;
        if (!_isSeekable)
            return PyResult.ValueError(PySR.Runtime_File_NotSeekable);
        // Discard StreamReader's internal buffer after seek to avoid stale data
        _reader?.DiscardBufferedData();
        _writer?.Flush();
        var newPos = _stream.Seek(offset, (SeekOrigin)whence);
        return PyIntObject.FromInteger(newPos);
    }

    internal PyResult Tell()
    {
        var check = CheckClosed();
        if (check.IsError) return check;
        if (!_isSeekable)
            return PyResult.ValueError(PySR.Runtime_File_NotSeekable);
        return PyIntObject.FromInteger(_stream.Position);
    }

    internal PyResult ReadLine(int size = -1)
    {
        var check = CheckClosed();
        if (check.IsError) return check;
        if (!_isReadable)
            return PyResult.ValueError(PySR.Runtime_File_NotReadable);

        if (_isTextMode)
        {
            Debug.Assert(_reader is not null);
            // Build the line character by character to preserve trailing newline.
            // StreamReader.ReadLine() strips newlines, which differs from Python semantics.
            var lineBuilder = new StringBuilder();
            int charsRead = 0;
            int ch;
            while ((ch = _reader.Read()) >= 0 && (size < 0 || charsRead < size))
            {
                lineBuilder.Append((char)ch);
                charsRead++;
                if (ch is '\n')
                    break;
            }
            return charsRead is 0
                ? (PyResult)PyResult.StopIteration()
                : (PyResult)PyStrObject.FromString(lineBuilder.ToString());
        }
        else
        {
            using var ms = new MemoryStream();
            int b;
            int total = 0;
            while ((b = _stream.ReadByte()) >= 0 && (size < 0 || total < size))
            {
                ms.WriteByte((byte)b);
                total++;
                if (b is '\n')
                    break;
            }
            return total is 0
                ? (PyResult)PyResult.StopIteration()
                : (PyResult)PyBytesObject.FromBytes(ms.ToArray());
        }
    }
}

[PyType("_io.FileObject")]
public sealed partial class PyFileObjectType : PyTypeObject<PyFileObject>
{
    protected override PyResult Repr(PyCallContext context, PyFileObject self)
    {
        return PyStrObject.FromString($"<_io.FileObject name='{self._name}' mode='{self._mode}'>");
    }

    protected override PyResult Enter(PyCallContext context, PyFileObject self)
    {
        var check = self.CheckClosed();
        if (check.IsError) return check;
        return self;
    }

    protected override PyResult Exit(PyCallContext context, PyFileObject self, PyObject excType, PyObject excVal, PyObject excTb)
    {
        return self.Close();
    }

    protected override PyResult Iter(PyCallContext context, PyFileObject self)
    {
        var check = self.CheckClosed();
        if (check.IsError) return check;
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyFileObject self)
    {
        return self.ReadLine();
    }

    [PyMethod("read")]
    [PyFunctionParameters("size=-1")]
    private static PyResult Read(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        var sizeObj = arguments[0];
        if (sizeObj is PyIntObject intObj)
            return self.Read(context, intObj.Int32Value);
        return self.Read(context);
    }

    [PyMethod("write")]
    [PyFunctionParameters("data")]
    private static PyResult Write(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        return self.Write(context, arguments[0]);
    }

    [PyMethod("close")]
    [PyFunctionParameters()]
    private static PyResult Close(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        return self.Close();
    }

    [PyMethod("flush")]
    [PyFunctionParameters()]
    private static PyResult Flush(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        return self.Flush();
    }

    [PyMethod("seek")]
    [PyFunctionParameters("offset", "whence=0")]
    private static PyResult Seek(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        var offset = arguments[0];
        var whence = arguments[1];
        if (offset is not PyIntObject offsetInt)
            return PyResult.TypeError(PySR.Runtime_File_SeekArg1NotInt, offset.PyType.FullName);
        var offsetVal = (long)offsetInt.Int32Value;
        int whenceVal = 0;
        if (whence is PyIntObject wInt)
            whenceVal = wInt.Int32Value;
        return self.Seek(offsetVal, whenceVal);
    }

    [PyMethod("tell")]
    [PyFunctionParameters()]
    private static PyResult Tell(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        return self.Tell();
    }

    [PyMethod("readline")]
    [PyFunctionParameters("size=-1")]
    private static PyResult ReadLine(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        var sizeObj = arguments[0];
        if (sizeObj is PyIntObject intObj)
            return self.ReadLine(intObj.Int32Value);
        return self.ReadLine();
    }

    [PyMethod("readable")]
    [PyFunctionParameters()]
    private static PyResult Readable(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self._isReadable);
    }

    [PyMethod("writable")]
    [PyFunctionParameters()]
    private static PyResult Writable(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self._isWritable);
    }

    [PyMethod("seekable")]
    [PyFunctionParameters()]
    private static PyResult Seekable(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        return PyBoolObject.FromBoolean(self._isSeekable);
    }

    [PyProperty("closed")]
    private static PyResult Get_closed(PyCallContext context, PyFileObject self)
    {
        return PyBoolObject.FromBoolean(self.IsClosed);
    }

    [PyProperty("mode")]
    private static PyResult Get_mode(PyCallContext context, PyFileObject self)
    {
        return PyStrObject.FromString(self._mode);
    }

    [PyProperty("name")]
    private static PyResult Get_name(PyCallContext context, PyFileObject self)
    {
        return PyStrObject.FromString(self._name);
    }
}

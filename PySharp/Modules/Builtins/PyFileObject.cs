using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Numerics;
using System.Text;
namespace PySharp.Modules.Builtins;

/// <summary>
/// Python file object returned by open().
/// Wraps a .NET Stream and provides text/binary file I/O.
/// </summary>
[AIGenerated]
public sealed class PyFileObject : PyObject, IDisposable
{
    private readonly Stream _stream;
    internal readonly bool _isTextMode;
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

        // leaveOpen: this object alone owns the stream lifetime, so
        // disposing the reader must not close it under the writer
        // (r+ builds both wrappers over the one stream)
        if (isTextMode && isReadable)
            _reader = new StreamReader(stream, PyEnvironmentHost.Utf8NoBom, detectEncodingFromByteOrderMarks: false, bufferSize: -1, leaveOpen: true);
        if (isTextMode && isWritable)
            _writer = new StreamWriter(stream, PyEnvironmentHost.Utf8NoBom, bufferSize: 1024, leaveOpen: true);
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
        if (check.IsError)
            return check;
        if (!_isReadable)
            return PyResult.ValueError(PySR.Runtime_File_NotReadable);

        if (_isTextMode)
        {
            Debug.Assert(_reader is not null);
            string result;
            if (size < 0)
            {
                result = _reader.ReadToEnd();
            }
            else if (size is 0)
            {
                result = string.Empty;
            }
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
        if (check.IsError)
            return check;
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
        try
        {
            // flush pending writer output while the stream can still take
            // it, then dispose the stream once (single owner)
            _writer?.Flush();
            _stream.Dispose();
        }
        catch (IOException ex)
        {
            return OSErrorFromIo(ex, _name);
        }
        finally
        {
            _reader = null;
            _writer = null;
        }
        return PyNoneObject.None;
    }

    // host-side deterministic cleanup mirroring CPython's close-on-__del__:
    // same flush/dispose path as Close() with its error swallowing; repeat
    // calls are no-ops once _closed is set
    public void Dispose()
    {
        if (!_closed)
            Close();
    }

    // CPython reports OS-level file errors as OSError subclasses in the
    // "[Errno N] strerror: 'path'" format; raw System.IO exceptions must
    // never escape the interpreter
    internal static PyResult OSErrorFromIo(Exception exception, string path)
    {
        return exception switch
        {
            FileNotFoundException or DirectoryNotFoundException =>
                PyResult.RaiseException(PyFileNotFoundErrorObjectType.Shared, PySR.Runtime_Os_FileNotFoundErrno, path),
            _ => PyResult.RaiseException(PyPermissionErrorObjectType.Shared, PySR.Runtime_Os_PermissionDeniedErrno, path),
        };
    }

    internal PyResult Flush()
    {
        var check = CheckClosed();
        if (check.IsError)
            return check;
        try
        {
            _writer?.Flush();
            _stream.Flush();
        }
        catch (IOException ex)
        {
            return OSErrorFromIo(ex, _name);
        }
        return PyNoneObject.None;
    }

    internal PyResult Seek(long offset, int whence)
    {
        // BufferedReader validates the whence value before the closed check;
        // the text layer (TextIOWrapper) checks closed first
        if (!_isTextMode && whence is not 0 and not 1 and not 2)
            return PyResult.ValueError(PySR.Runtime_File_WhenceUnsupported, whence);
        if (_closed)
        {
            return PyResult.ValueError(_isTextMode
                ? PySR.Runtime_File_Closed
                : PySR.Runtime_File_SeekClosed);
        }
        if (!_isSeekable)
            return PyResult.ValueError(PySR.Runtime_File_NotSeekable);
        if (_isTextMode)
        {
            if (whence is not 0 and not 1 and not 2)
                return PyResult.ValueError(PySR.Runtime_File_InvalidWhence, whence);
            // only SEEK_SET can reach a negative position; the cur/end
            // relative forms reject nonzero offsets before this point in
            // CPython and stay supported here
            if (whence is 0 && offset < 0)
                return PyResult.ValueError(PySR.Runtime_File_NegativeSeekPosition, offset);
        }
        else
        {
            // compute the lseek target like the OS so a position before the
            // start of the file surfaces as OSError EINVAL, not a raw .NET
            // error; -offset wraps harmlessly for long.MinValue
            long target = whence is 1 ? _stream.Position : whence is 2 ? _stream.Length : 0;
            if (offset < 0 && target < -offset)
                return PyResult.OSError(PySR.Runtime_Os_InvalidArgumentErrno);
        }
        try
        {
            // Discard StreamReader's internal buffer after seek to avoid stale data
            _reader?.DiscardBufferedData();
            _writer?.Flush();
            var newPos = _stream.Seek(offset, (SeekOrigin)whence);
            return PyIntObject.FromInteger(newPos);
        }
        catch (IOException ex)
        {
            return OSErrorFromIo(ex, _name);
        }
    }

    internal PyResult Tell()
    {
        if (_closed)
        {
            return PyResult.ValueError(_isTextMode
                ? PySR.Runtime_File_Closed
                : PySR.Runtime_File_ClosedNoPeriod);
        }
        if (!_isSeekable)
            return PyResult.ValueError(PySR.Runtime_File_NotSeekable);
        return PyIntObject.FromInteger(_stream.Position);
    }

    internal PyResult ReadLine(int size = -1)
    {
        var check = CheckClosed();
        if (check.IsError)
            return check;
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
                ? (PyResult)PyStrObject.Empty
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
                ? (PyResult)PyBytesObject.FromBytes([])
                : (PyResult)PyBytesObject.FromBytes(ms.ToArray());
        }
    }
}

[AIGenerated]
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
        if (check.IsError)
            return check;
        return self;
    }

    protected override PyResult Exit(PyCallContext context, PyFileObject self, PyObject excType, PyObject excVal, PyObject excTb)
    {
        return self.Close();
    }

    protected override PyResult Iter(PyCallContext context, PyFileObject self)
    {
        var check = self.CheckClosed();
        if (check.IsError)
            return check;
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyFileObject self)
    {
        var result = self.ReadLine();
        if (result.IsError)
            return result;
        // readline() returns ''/b'' at EOF; iteration must raise StopIteration.
        if (result.Value is PyStrObject str && str.Value.Length is 0)
            return PyResult.StopIteration();
        if (result.Value is PyBytesObject bytes && bytes.AsSpan().Length is 0)
            return PyResult.StopIteration();
        return result;
    }

    [PyMethod("read")]
    [PyFunctionParameters("size=-1", "/")]
    private static PyResult Read(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        var sizeObj = arguments[0];
        if (sizeObj is PyIntObject intObj)
            return self.Read(context, intObj.Int32Value);
        return self.Read(context);
    }

    [PyMethod("write")]
    [PyFunctionParameters("data", "/")]
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
    [PyFunctionParameters("offset", "whence=0", "/")]
    private static PyResult Seek(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        // PyNumber_AsOff_t / clinic int: both arguments go through __index__
        var offsetInt = IndexOrError(context, arguments[0], out var offsetError);
        if (offsetInt is null)
            return offsetError;
        var whenceInt = IndexOrError(context, arguments[1], out var whenceError);
        if (whenceInt is null)
            return whenceError;

        // off_t is a 64-bit C type; the text layer surfaces the failed
        // conversion from the OS (EINVAL) while buffered raises ValueError
        var bigOffset = offsetInt.Value;
        if (bigOffset > long.MaxValue || bigOffset < long.MinValue)
        {
            return self._isTextMode
                ? PyResult.OSError(PySR.Runtime_Os_InvalidArgumentErrno)
                : PyResult.ValueError(PySR.Runtime_File_CannotFitOffset);
        }
        return self.Seek((long)bigOffset, whenceInt.Int32Value);
    }

    private static PyIntObject? IndexOrError(PyCallContext context, PyObject obj, out PyResult error)
    {
        if (obj is PyIntObject intObj)
        {
            error = default;
            return intObj;
        }
        if (obj.PyType.Slots.Index is null)
        {
            error = PyResult.TypeError(PySR.Runtime_Number_Int_CannotInterpretedAsInt, obj.PyType.FullName);
            return null;
        }
        var indexResult = PySpecialMethods.Index(context, obj);
        if (indexResult.IsError)
        {
            error = indexResult;
            return null;
        }
        error = default;
        return indexResult.Value;
    }

    [PyMethod("tell")]
    [PyFunctionParameters()]
    private static PyResult Tell(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        return self.Tell();
    }

    [PyMethod("readline")]
    [PyFunctionParameters("size=-1", "/")]
    private static PyResult ReadLine(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        var sizeObj = arguments[0];
        if (sizeObj is PyIntObject intObj)
            return self.ReadLine(intObj.Int32Value);
        return self.ReadLine();
    }

    [PyMethod("readlines")]
    [PyFunctionParameters("hint=-1", "/")]
    private static PyResult ReadLines(PyCallContext context, PyFileObject self, PyArguments arguments)
    {
        // CPython _IOBase.readlines: keep readline() until EOF, stopping
        // once the accumulated size EXCEEDS a positive hint (the crossing
        // line is kept); text mode counts characters, binary mode bytes.
        BigInteger hint = -1;
        if (arguments[0] is PyIntObject hintObj)
            hint = hintObj.Value;

        var lines = new List<PyObject>();
        long length = 0;
        while (true)
        {
            var lineResult = self.ReadLine();
            if (lineResult.IsError)
                return lineResult;

            var line = lineResult.Value;
            var lineLength = line switch
            {
                PyStrObject s => s.Value.Length,
                PyBytesObject b => b.Length,
                _ => 0,
            };
            if (lineLength is 0)
                break;

            lines.Add(line);
            length += lineLength;
            if (hint > 0 && length > hint)
                break;
        }

        return PyListObject.CreateList(lines);
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

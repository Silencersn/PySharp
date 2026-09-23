using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Numerics;
using System.Text;
namespace PySharp.Modules.IO;

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

    // TextIOWrapper newline=None translation state. Raw characters go through
    // this buffer so a '\r' at a chunk boundary still swallows the '\n' that
    // follows it in the next chunk.
    private readonly char[] _textBuffer = new char[1024];
    private int _textCount;
    private int _textIndex;
    private bool _pendingLineFeed;

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
            return PyStrObject.FromString(ReadText(size));
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
                return PyResult.TypeError(PySR.Runtime_File_WriteNeedStr, data.PyType.TpName);
            _writer.Write(TranslateForWrite(strObj.Value));
            _writer.Flush();
            return PyIntObject.FromInteger(strObj.Value.Length);
        }
        else
        {
            if (data is not PyBytesObject bytesObj)
                return PyResult.TypeError(PySR.Runtime_File_WriteNeedBytes, data.PyType.TpName);
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
            // TextIOWrapper accepts only zero-offset cur/end-relative seeks:
            // nonzero forms raise io.UnsupportedOperation instead of reaching
            // the underlying stream (Modules/_io/textio.c), seek(0, 1)
            // resyncs to the current position and seek(0, 2) jumps to EOF.
            if (whence is 1 && offset is not 0)
                return PyResult.RaiseException(PyUnsupportedOperationObjectType.Shared, PySR.Runtime_File_CurRelativeSeekUnsupported);
            if (whence is 2 && offset is not 0)
                return PyResult.RaiseException(PyUnsupportedOperationObjectType.Shared, PySR.Runtime_File_EndRelativeSeekUnsupported);
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
            _textCount = 0;
            _textIndex = 0;
            _pendingLineFeed = false;
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

    // TextIOWrapper keeps newline=None as the default mode: text reads fold
    // '\r\n' and '\r' into '\n' (universal newlines) while text writes expand
    // '\n' into os.linesep, which Environment.NewLine mirrors
    // (Modules/_io/textio.c: set_newline + _io_TextIOWrapper_write_impl).
    private static string TranslateForWrite(string text)
    {
        var lineSeparator = Environment.NewLine;
        if (lineSeparator.Length is 1 || !text.Contains('\n'))
            return text;
        return text.Replace("\n", lineSeparator);
    }

    private int ReadRawChar()
    {
        if (_textIndex >= _textCount)
        {
            _textCount = _reader!.Read(_textBuffer, 0, _textBuffer.Length);
            _textIndex = 0;
            if (_textCount <= 0)
                return -1;
        }
        return _textBuffer[_textIndex++];
    }

    private int ReadTranslatedChar()
    {
        while (true)
        {
            var ch = ReadRawChar();
            if (ch < 0)
            {
                _pendingLineFeed = false;
                return -1;
            }
            if (_pendingLineFeed)
            {
                _pendingLineFeed = false;
                if (ch is '\n')
                    continue;
            }
            if (ch is '\r')
            {
                _pendingLineFeed = true;
                return '\n';
            }
            return ch;
        }
    }

    private string ReadText(int size)
    {
        if (size is 0)
            return string.Empty;
        var builder = new StringBuilder();
        while (size < 0 || builder.Length < size)
        {
            var ch = ReadTranslatedChar();
            if (ch < 0)
                break;
            builder.Append((char)ch);
        }
        return builder.ToString();
    }

    private string ReadTextLine(int size)
    {
        var builder = new StringBuilder();
        while (size < 0 || builder.Length < size)
        {
            var ch = ReadTranslatedChar();
            if (ch < 0)
                break;
            builder.Append((char)ch);
            if (ch is '\n')
                break;
        }
        return builder.ToString();
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
            return PyStrObject.FromString(ReadTextLine(size));
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
// module position "_io" like CPython's io stack types (the qual name stays
// bare; ReprName composes it as "_io.FileObject" from the module)
[PyType("FileObject", Module = "_io")]
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
            error = PyResult.TypeError(PySR.Runtime_Number_Int_CannotInterpretedAsInt, obj.PyType.TpName);
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

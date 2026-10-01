using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Numerics;
using System.Text;
namespace PySharp.Modules.IO;

/// <summary>
/// Write-buffering policy of the standard streams, the decision CPython's
/// create_stdio makes (Python/pylifecycle.c): line buffering on a terminal
/// and for stderr however it is redirected, the 8192-byte block default when
/// stdout is redirected. open() files and directly constructed wrappers keep
/// write-through, the historical per-write flush behavior.
/// </summary>
internal enum PyStandardStreamBuffering
{
    WriteThrough,
    Line,
    Block,
}

/// <summary>
/// Python text file object: the result of open() in text mode and the wrapper
/// around sys.stdin/sys.stdout/sys.stderr. Wraps a .NET Stream and provides
/// text I/O; binary-mode open() results share the layout via
/// <see cref="_isTextMode"/>. CPython models both as _io.TextIOWrapper, so the
/// standard streams and open() share this one type object.
/// </summary>
[AIGenerated]
public sealed class PyTextIOWrapperObject : PyObject, IDisposable
{
    private readonly Stream _stream;
    internal readonly bool _isTextMode;
    internal readonly bool _isReadable;
    internal readonly bool _isWritable;
    internal readonly bool _isSeekable;
    // open() owns its handle and closes it; the standard streams do not own the
    // process's console handles, so close() only marks the wrapper closed
    private readonly bool _ownsStream;
    internal readonly string _mode;
    internal readonly string _name;
    private bool _closed;

    // Text codec and newline mode (open()'s encoding=/errors=/newline=).
    // Binary files carry nulls and keep the raw byte paths.
    private readonly PyTextCodec? _codec;
    private readonly string _encodingParam;   // name the codec resolves from
    internal readonly string _encodingName;   // canonical name, f.encoding
    internal readonly string _errorsName;     // f.errors
    private readonly string? _newline;        // null = universal newlines

    // Incremental read state. Raw bytes are pulled in chunks and decoded one
    // code point at a time so the stream offset of every produced character
    // is known — TextIOWrapper.tell() reports it as a seek cookie and
    // seek(cookie) rebuilds the exact read state from it.
    private readonly byte[] _rawBuf = new byte[8192];
    private int _rawStart, _rawEnd;
    private long _rawBufBase;                 // stream offset of _rawBuf[0]
    private bool _rawEof;
    private readonly char[] _charBuf = new char[64];
    private readonly int[] _charLens = new int[64];
    private int _charStart, _charEnd;
    private long _frontPos;                   // stream offset of _charBuf[_charStart]
    private bool _pendingCR;                  // a '\r' was consumed; in universal-newline
                                              // mode it still owes a '\n' character
    private int _pendingCRLen;
    private bool _bomChecked;
    private readonly StringBuilder _staging = new(4);
    // BOM-emitting codecs (utf-8-sig, utf-16, utf-32) prepend their preamble
    // to the first write only, like TextIOWrapper's incremental encoder
    private bool _wrotePreamble;

    // Standard-stream write buffering. The buffer holds encoded bytes and
    // lands on the raw stream per line, per ~8 KiB, and at flush()/close()
    // /shutdown; one 8192-byte threshold stands in for CPython's two-level
    // pipeline (text pending_bytes 8192 + BufferedWriter 8192), which is
    // order-equivalent for everything the standard streams expose.
    internal readonly PyStandardStreamBuffering _buffering;
    internal readonly bool _isTerminal;
    private readonly Lock _writeLock = new();
    private readonly byte[] _writeBuf = new byte[8192];
    private int _writeLen;

    internal PyTextIOWrapperObject(Stream stream, string mode, string name,
        bool isTextMode, bool isReadable, bool isWritable, bool isSeekable,
        string? encoding = null, string? errors = null, string? newline = null,
        PyTextCodec? codec = null, bool wrotePreamble = false, bool ownsStream = true,
        PyStandardStreamBuffering buffering = PyStandardStreamBuffering.WriteThrough,
        bool isTerminal = false)
    {
        _stream = stream;
        _mode = mode;
        _name = name;
        _isTextMode = isTextMode;
        _isReadable = isReadable;
        _isWritable = isWritable;
        _isSeekable = isSeekable;
        _ownsStream = ownsStream;
        _encodingParam = !isTextMode ? string.Empty : encoding ?? "utf-8";
        _errorsName = !isTextMode ? string.Empty : errors ?? "strict";
        _encodingName = _encodingParam;
        _newline = newline;
        if (isTextMode)
            _codec = codec ?? PyTextCodec.Create(_encodingParam, _errorsName, out _);
        // appending to a non-empty file suppresses the codec preamble, like
        // CPython's _textiowrapper_fix_encoder_state
        _wrotePreamble = wrotePreamble;
        _buffering = buffering;
        _isTerminal = isTerminal;
        // a non-seekable stream (the standard streams) has no position to read
        var position = stream.CanSeek ? stream.Position : 0;
        _rawBufBase = position;
        _codec?.PositionBase = _rawBufBase;
        _frontPos = position;
    }

    /// <summary>
    /// Wraps one of the environment's standard streams as CPython's
    /// create_stdio does: a TextIOWrapper over the raw host stream, with
    /// universal newlines, the environment's encoding and the per-stream
    /// error handler (stderr uses backslashreplace). The handle stays owned by
    /// the host, so close() never disposes it. The buffering mode carries
    /// create_stdio's decision: line buffering on a terminal, the block
    /// default when redirected.
    /// </summary>
    internal static PyTextIOWrapperObject CreateStandardStream(
        Stream stream, string name, string mode, bool readable, bool writable,
        Encoding encoding, string errors,
        PyStandardStreamBuffering buffering = PyStandardStreamBuffering.WriteThrough,
        bool isTerminal = false)
    {
        var encodingName = PyTextCodec.CanonicalEncodingName(encoding);
        return new PyTextIOWrapperObject(stream, mode, name,
            isTextMode: true, isReadable: readable, isWritable: writable,
            isSeekable: stream.CanSeek,
            encoding: encodingName, errors: errors, newline: null,
            codec: PyTextCodec.Create(encodingName, errors, out _),
            ownsStream: false, buffering: buffering, isTerminal: isTerminal);
    }

    public override PyTypeObject DefaultPyType => PyTextIOWrapperObjectType.Shared;
    internal bool IsClosed => _closed;

    internal PyResult CheckClosed()
    {
        if (_closed)
            return PyResult.ValueError(PySR.Runtime_File_Closed);
        return default;
    }

    // the flag queries use CPython's no-period closed message
    internal PyResult CheckClosedNoPeriod()
    {
        if (_closed)
            return PyResult.ValueError(PySR.Runtime_File_ClosedNoPeriod);
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
            Debug.Assert(_codec is not null);
            return ReadText(size);
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
                // cap the allocation by the bytes actually left; a size near
                // int.MaxValue must not kill the process in new byte[]
                long remaining = _stream.Length - _stream.Position;
                var count = (int)Math.Min(size, Math.Max(0, remaining));
                var buf = new byte[count];
                var read = _stream.Read(buf, 0, count);
                if (read < count)
                    Array.Resize(ref buf, read);
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
            if (data is not PyStrObject strObj)
                return PyResult.TypeError(PySR.Runtime_File_WriteNeedStr, data.PyType.TpName);
            // the line-flush decision reads the pre-translation text like
            // CPython does (Modules/_io/textio.c: haslf, and a bare '\r'
            // flushes too)
            var needLineFlush = _buffering is PyStandardStreamBuffering.Line &&
                (strObj.Value.Contains('\n') || strObj.Value.Contains('\r'));
            var text = TranslateForWrite(strObj.Value);
            var encoded = PyStrObjectType.EncodeCore(context, text, _encodingParam, _errorsName,
                emitPreamble: !_wrotePreamble);
            if (encoded.IsError)
                return encoded;
            var bytes = ((PyBytesObject)encoded.Value).AsSpan();
            try
            {
                if (_buffering is PyStandardStreamBuffering.WriteThrough)
                {
                    // flush per write like the old StreamWriter pipeline: a
                    // leaked file object may never be closed, and readers in
                    // the same process must see prior writes
                    _stream.Write(bytes);
                    _stream.Flush();
                }
                else
                {
                    WriteBuffered(bytes, needLineFlush);
                }
            }
            catch (IOException ex)
            {
                return OSErrorFromIo(ex, _name);
            }
            _wrotePreamble = true;
            // an update-mode file continues reading from the post-write
            // position; drop any pre-write readahead
            if (_isReadable)
                ResetTextReadState(_stream.Position, pendingCR: false);
            return PyIntObject.FromInteger(CodePointLength(strObj.Value));
        }
        else
        {
            if (data is not PyBytesObject bytesObj)
                return PyResult.TypeError(PySR.Runtime_File_WriteNeedBytes, data.PyType.TpName);
            var span = bytesObj.AsSpan();
            try
            {
                if (_buffering is PyStandardStreamBuffering.WriteThrough)
                {
                    _stream.Write(span);
                    _stream.Flush();
                }
                else
                {
                    WriteBuffered(span, needLineFlush: false);
                }
            }
            catch (IOException ex)
            {
                return OSErrorFromIo(ex, _name);
            }
            return PyIntObject.FromInteger(span.Length);
        }
    }

    // buffered write path for the standard streams: the caller's bytes join
    // the pending buffer, which lands when the 8192 threshold fills, when a
    // line-buffered write carried a newline, or at an explicit flush
    private void WriteBuffered(ReadOnlySpan<byte> bytes, bool needLineFlush)
    {
        lock (_writeLock)
        {
            // no GIL: interpreter threads share the wrapper, so the buffer
            // hand-off to the raw stream must not interleave mid-chunk
            if (_writeLen > 0 && _writeLen + bytes.Length > _writeBuf.Length)
                WriteBufferToStream();
            if (bytes.Length >= _writeBuf.Length)
            {
                _stream.Write(bytes);
                _stream.Flush();
            }
            else
            {
                bytes.CopyTo(_writeBuf.AsSpan(_writeLen));
                _writeLen += bytes.Length;
                if (_writeLen >= _writeBuf.Length)
                    WriteBufferToStream();
            }
            if (needLineFlush && _writeLen > 0)
                WriteBufferToStream();
        }
    }

    // caller holds _writeLock
    private void WriteBufferToStream()
    {
        _stream.Write(_writeBuf.AsSpan(0, _writeLen));
        _stream.Flush();
        _writeLen = 0;
    }

    private void FlushWriteBuffer()
    {
        lock (_writeLock)
        {
            if (_writeLen > 0)
                WriteBufferToStream();
        }
    }

    internal PyResult Close()
    {
        if (_closed)
            return PyNoneObject.None;
        _closed = true;
        if (!_ownsStream)
        {
            // a standard stream: the host owns the handle, so a final flush
            // must not tear the process's console down, and a failing flush
            // must not mask the successful close
            if (_isWritable)
            {
                try
                {
                    FlushWriteBuffer();
                    _stream.Flush();
                }
                catch (IOException)
                {
                }
            }
            return PyNoneObject.None;
        }
        try
        {
            _stream.Dispose();
        }
        catch (IOException ex)
        {
            return OSErrorFromIo(ex, _name);
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

    // CPython counts characters (code points) for write()'s return value; a
    // surrogate pair is one character, not two UTF-16 units
    internal static int CodePointLength(string value)
    {
        int length = value.Length;
        for (int i = 0; i < value.Length - 1; i++)
        {
            if (char.IsHighSurrogate(value[i]) && char.IsLowSurrogate(value[i + 1]))
                length--;
        }
        return length;
    }

    // CPython reports OS-level file errors as OSError subclasses in the
    // "[Errno N] strerror: 'path'" format; raw System.IO exceptions must
    // never escape the interpreter
    internal static PyResult OSErrorFromIo(Exception exception, string path)
    {
        return exception switch
        {
            FileNotFoundException or DirectoryNotFoundException =>
                PyUtils.OSErrorFromErrno(PyFileNotFoundErrorObjectType.Shared, 2, PySR.Runtime_Os_StrerrorFileNotFound, path),
            _ => PyUtils.OSErrorFromErrno(PyPermissionErrorObjectType.Shared, 13, PySR.Runtime_Os_StrerrorPermissionDenied, path),
        };
    }

    internal PyResult Flush()
    {
        var check = CheckClosed();
        if (check.IsError)
            return check;
        try
        {
            FlushWriteBuffer();
            _stream.Flush();
        }
        catch (IOException ex)
        {
            return OSErrorFromIo(ex, _name);
        }
        return PyNoneObject.None;
    }

    // flush_std_files (Python/pylifecycle.c): finalization drains the
    // standard streams' buffers before the raw handles go away, tolerating
    // failures the way the standard streams' close does; unlike close() it
    // must not mark the wrapper closed
    internal void FlushAtShutdown()
    {
        if (_closed || !_isWritable)
            return;
        try
        {
            FlushWriteBuffer();
            _stream.Flush();
        }
        catch (IOException)
        {
        }
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
        {
            // TextIOWrapper: _unsupported(self->state, "underlying stream is
            // not seekable") — only the standard streams reach this today
            return _isTextMode
                ? PyResult.RaiseException(PyUnsupportedOperationObjectType.Shared, PySR.Runtime_File_UnderlyingNotSeekable)
                : PyResult.ValueError(PySR.Runtime_File_NotSeekable);
        }
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
            // error; -offset wraps for long.MinValue, which must read as the
            // most negative offset there is
            long target = whence is 1 ? _stream.Position : whence is 2 ? _stream.Length : 0;
            if (offset < 0 && (offset is long.MinValue || target < -offset))
                return PyUtils.OSErrorFromErrno(PyOSErrorObjectType.Shared, 22, PySR.Runtime_Os_StrerrorInvalidArgument);
        }
        try
        {
            if (_isTextMode)
                return SeekText(offset, whence);
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
        {
            return _isTextMode
                ? PyResult.RaiseException(PyUnsupportedOperationObjectType.Shared, PySR.Runtime_File_UnderlyingNotSeekable)
                : PyResult.ValueError(PySR.Runtime_File_NotSeekable);
        }
        if (!_isTextMode)
            return PyIntObject.FromInteger(_stream.Position);
        // TextIOWrapper.tell(): an opaque cookie that seek() accepts back.
        // The low bits carry the stream offset of the next character to be
        // returned (a clean decoder state makes it the byte offset, so the
        // familiar ASCII values match CPython literally); bit 62 marks a
        // pending translated '\r' (see PackCookie).
        if (!_isReadable)
            return PyIntObject.FromInteger(_stream.Position);
        bool flag = _pendingCR && _charStart >= _charEnd;
        // with the flag set the pending '\r''s bytes sit at _frontPos and
        // are already accounted for by the flag, so the cookie points past
        // them; a clean state packs the bare offset
        return PyIntObject.FromInteger(PackCookie(flag ? _frontPos + _pendingCRLen : _frontPos, flag));
    }

    // cookie layout: bit 62 = a '\r' was consumed in universal-newline mode
    // and its translated '\n' (or the '\n' it would swallow) is still owed;
    // bit 63 stays clear so cookies remain positive longs. A clean state
    // packs to the bare byte offset.
    private const long CookieFlagMask = 1L << 62;

    private static long PackCookie(long pos, bool flag) => flag ? pos | CookieFlagMask : pos;

    // TextIOWrapper keeps newline=None as the default mode: text reads fold
    // '\r\n' and '\r' into '\n' (universal newlines) while text writes expand
    // '\n' into os.linesep, which Environment.NewLine mirrors
    // (Modules/_io/textio.c: set_newline + _io_TextIOWrapper_write_impl).
    // Explicit newline values write that terminator instead; '' and '\n'
    // leave the text unchanged.
    private string TranslateForWrite(string text)
    {
        if (_newline is not null)
        {
            if (_newline is "" or "\n" || !text.Contains('\n'))
                return text;
            return text.Replace("\n", _newline);
        }
        var lineSeparator = Environment.NewLine;
        if (lineSeparator.Length is 1 || !text.Contains('\n'))
            return text;
        return text.Replace("\n", lineSeparator);
    }

    // ---- incremental text read machinery ----

    private void ResetTextReadState(long pos, bool pendingCR)
    {
        _rawStart = 0;
        _rawEnd = 0;
        _rawBufBase = pos;
        _rawEof = false;
        _codec?.ResetGenericDecoder();
        _charStart = 0;
        _charEnd = 0;
        _frontPos = pos;
        _pendingCR = pendingCR;
        _pendingCRLen = 0;
        // utf-8-sig strips the BOM again after a seek back to the start,
        // and sniffed utf-16/32 re-recognize theirs
        _bomChecked = pos is not 0;
        if (pos is 0)
            _codec?.ResetBomSniff();
        else
            _codec?.MarkBomSniffedIfUnsniffed();
        _codec?.PositionBase = _rawBufBase;
    }

    // bytes for the decoder: refills the raw buffer from the stream
    private PyResult? ReadMoreRaw()
    {
        if (_rawEnd == _rawBuf.Length && _rawStart > 0)
        {
            Array.Copy(_rawBuf, _rawStart, _rawBuf, 0, _rawEnd - _rawStart);
            _rawEnd -= _rawStart;
            _rawBufBase += _rawStart;
            _rawStart = 0;
            _codec?.PositionBase = _rawBufBase;
        }
        int read;
        try
        {
            read = _stream.Read(_rawBuf, _rawEnd, _rawBuf.Length - _rawEnd);
        }
        catch (IOException ex)
        {
            return OSErrorFromIo(ex, _name);
        }
        if (read is 0)
        {
            _rawEof = true;
            return null;
        }
        _rawEnd += read;
        return null;
    }

    // utf-8-sig: the BOM check needs the first three bytes (or EOF)
    private bool NeedsBomBytes => _codec!.StripUtf8Bom && !_bomChecked &&
        _rawEnd - _rawStart < 3 && !_rawEof;

    private void CheckBom()
    {
        if (_codec!.StripUtf8Bom && !_bomChecked)
        {
            int avail = _rawEnd - _rawStart;
            if (avail >= 3)
            {
                if (_rawBuf[_rawStart] is 0xEF && _rawBuf[_rawStart + 1] is 0xBB && _rawBuf[_rawStart + 2] is 0xBF)
                {
                    _rawStart += 3;
                    // the stripped BOM still counts toward the byte offset tell()
                    // reports; seek(0) resets it and strips again
                    _frontPos += 3;
                }
                _bomChecked = true;
            }
            else if (_rawEof)
            {
                // a truncated BOM prefix at end-of-file is consumed like a
                // BOM: CPython's utf-8-sig decoder yields '' for it instead
                // of a truncated-sequence error
                bool bomPrefix = (avail is 1 && _rawBuf[_rawStart] is 0xEF) ||
                    (avail is 2 && _rawBuf[_rawStart] is 0xEF && _rawBuf[_rawStart + 1] is 0xBB);
                if (bomPrefix)
                {
                    _frontPos += avail;
                    _rawStart += avail;
                }
                _bomChecked = true;
            }
            // else: keep waiting for the third byte (NeedsBomBytes gates the
            // decode path so nothing consumes the partial BOM)
        }
    }

    // decodes one code point into the staging builder with universal-newline
    // processing; appends 0-2 queue slots. Returns false when the decode
    // needs more raw bytes (the caller refills); a decode error surfaces
    // through `error` with a true return.
    private bool DecodeIntoQueue(out PyResult? error)
    {
        error = null;
        int entryIndex = _rawStart;
        _staging.Clear();
        var span = _rawBuf.AsSpan(0, _rawEnd);
        int index = _rawStart;
        var status = _codec!.DecodeStep(span, ref index, eof: _rawEof, _staging, out error);
        _rawStart = index;
        if (status is PyDecodeStatus.NeedMore)
        {
            _rawStart = entryIndex;
            return false;
        }
        if (status is PyDecodeStatus.Pending)
        {
            // absorbed bytes live in the decoder state; commit the window
            // pointer so they are never re-fed
            _rawStart = index;
            return false;
        }
        if (status is PyDecodeStatus.EndOfInput)
        {
            // nothing was decoded; committing keeps any BOM skip so the
            // loop's EOF check terminates, and the consumed bytes count
            // toward the position tell() reports
            _frontPos += index - entryIndex;
            _rawStart = index;
            _rawEof = true;
            return false;
        }
        if (status is PyDecodeStatus.Error)
        {
            // the failed event's bytes count as consumed even when the
            // handler rejects, keeping tell() consistent so seek(tell())
            // does not replay them
            _frontPos += index - entryIndex;
            return true;
        }
        if (_codec!.HasPendingBomReject && index > entryIndex)
        {
            // BOM-less utf-16/32: the first native decode event came back,
            // so the stream layer's BOM rejection now fires (before the
            // decoded characters are delivered)
            var bomError = _codec.TakePendingBomReject(_rawBuf.AsSpan(0, _rawEnd), index);
            if (bomError is not null)
            {
                _frontPos += index - entryIndex;
                _rawStart = index;
                error = bomError;
                return true;
            }
        }
        if (_staging.Length is 0)
        {
            // an ignore-style handler consumed the bytes silently; they
            // still count toward the byte position tell() reports
            _frontPos += index - entryIndex;
            return true;
        }

        // universal newlines (newline=None): a raw '\r' owes a translated
        // '\n'; a '\n' immediately after it is swallowed. The deferred '\r'
        // is why tell() carries a cookie flag: the byte position alone says
        // the '\r' was consumed but not whether its '\n' was delivered.
        // (A raw '\r' is always a sole decode event; handler replacements
        // like backslashreplace output never open with one.)
        var byteLen = index - entryIndex;
        if (_newline is null)
        {
            if (_pendingCR)
            {
                _pendingCR = false;
                if (_staging.Length is 1 && _staging[0] is '\n')
                {
                    PushChar('\n', _pendingCRLen + byteLen);
                    return true;
                }
                PushChar('\n', _pendingCRLen);
            }
            // checked independently of the flush above: after resolving a
            // pending '\r' the next event may itself be a '\r' ("a\r\r\n"
            // folds to 'a\n\n'), which then owes its own '\n'
            if (_staging.Length is 1 && _staging[0] is '\r')
            {
                _pendingCR = true;
                _pendingCRLen = byteLen;
                return true;
            }
        }
        // an escape-style handler can emit several characters for one
        // event; only the first slot carries the consumed byte count
        for (int i = 0; i < _staging.Length; i++)
            PushChar(_staging[i], i is 0 ? byteLen : 0);
        return true;
    }

    // ensures the queue holds at least one character (or clean EOF). A
    // pending '\r' is resolved by decoding AHEAD — the translated '\n' is
    // queued before the following character (or combined with a swallowed
    // '\n'), which keeps both the character order and the byte positions
    // equal to CPython's; only a '\r' at end-of-file flushes directly.
    private PyResult? FillChar()
    {
        while (_charStart >= _charEnd)
        {
            if (_rawEof && _rawStart >= _rawEnd)
            {
                if (!_pendingCR)
                    return null;
                PushChar('\n', _pendingCRLen);
                _pendingCR = false;
                continue;
            }
            if (NeedsBomBytes)
            {
                var bomErr = ReadMoreRaw();
                if (bomErr is not null)
                    return bomErr.Value;
                CheckBom();
                continue;
            }
            CheckBom();
            if (_rawStart >= _rawEnd && !_rawEof)
            {
                var emptyErr = ReadMoreRaw();
                if (emptyErr is not null)
                    return emptyErr.Value;
                continue;
            }
            if (DecodeIntoQueue(out var error))
            {
                if (error is not null)
                    return error;
            }
            else
            {
                // a truncated sequence asked for more bytes; once the refill
                // lands at EOF the tail redecodes with eof set and surfaces
                // the codec's end-of-data error event
                var ioErr = ReadMoreRaw();
                if (ioErr is not null)
                    return ioErr.Value;
            }
        }
        return null;
    }

    private void PushChar(char ch, int byteLen)
    {
        if (_charEnd == _charBuf.Length)
        {
            // recycle the consumed prefix; the queue never holds more than
            // one decode event (a handler replacement is at most 16 chars),
            // so this keeps headroom well above the worst case
            Array.Copy(_charBuf, _charStart, _charBuf, 0, _charEnd - _charStart);
            Array.Copy(_charLens, _charStart, _charLens, 0, _charEnd - _charStart);
            _charEnd -= _charStart;
            _charStart = 0;
        }
        _charBuf[_charEnd] = ch;
        _charLens[_charEnd] = byteLen;
        _charEnd++;
    }

    // the next code point to return, consuming it; -1 at EOF. A surrogate
    // pair occupies two queue slots — the continuation slot carries a zero
    // byte length — and leaves together, so read(n) counts code points like
    // CPython and a tell() cookie never lands between the halves
    private PyResult? NextChar(out int ch)
    {
        var err = FillChar();
        if (err is not null)
        {
            ch = 0;
            return err.Value;
        }
        if (_charStart >= _charEnd)
        {
            ch = -1;
            return null;
        }
        ch = _charBuf[_charStart];
        _frontPos += _charLens[_charStart];
        _charStart++;
        if (ch is >= 0xD800 and <= 0xDBFF && _charStart < _charEnd && _charLens[_charStart] is 0)
        {
            ch = 0x10000 + ((ch - 0xD800) << 10) + (_charBuf[_charStart] - 0xDC00);
            _charStart++;
        }
        return null;
    }

    // lookahead without consuming; -1 at EOF
    private PyResult? PeekChar(out int ch)
    {
        var err = FillChar();
        if (err is not null)
        {
            ch = 0;
            return err.Value;
        }
        if (_charStart >= _charEnd)
        {
            ch = -1;
            return null;
        }
        ch = _charBuf[_charStart];
        return null;
    }

    private PyResult ReadText(int size)
    {
        if (size is 0)
            return PyStrObject.Empty;
        var builder = new StringBuilder();
        int read = 0;
        while (size < 0 || read < size)
        {
            var err = NextChar(out var ch);
            if (err is not null)
                return err.Value;
            if (ch < 0)
                break;
            PyTextCodec.AppendCodePoint(builder, ch);
            read++;
        }
        return PyStrObject.FromString(builder.ToString());
    }

    // readline() terminator per newline mode (Modules/_io/textio.c): None
    // folds \r\n|\r to '\n' first and terminates on it; '' detects all three
    // terminators but returns them untranslated; a fixed value terminates
    // only on itself and translates nothing
    private PyResult ReadTextLine(int size)
    {
        var builder = new StringBuilder();
        int read = 0;
        while (size < 0 || read < size)
        {
            var err = NextChar(out var ch);
            if (err is not null)
                return err.Value;
            if (ch < 0)
                break;
            PyTextCodec.AppendCodePoint(builder, ch);
            read++;
            if (_newline is null)
            {
                if (ch is '\n')
                    break;
            }
            else if (_newline is "")
            {
                if (ch is '\n')
                    break;
                if (ch is '\r')
                {
                    var peek = PeekChar(out var next);
                    if (peek is not null)
                        return peek.Value;
                    if (next is '\n')
                    {
                        // the '\n' completes the terminator only while the
                        // size budget lasts; otherwise it starts the next line
                        if (size < 0 || read < size)
                        {
                            err = NextChar(out next);
                            if (err is not null)
                                return err.Value;
                            PyTextCodec.AppendCodePoint(builder, next);
                        }
                        break;
                    }
                    break;
                }
            }
            else if (_newline is "\n")
            {
                if (ch is '\n')
                    break;
            }
            else if (_newline is "\r")
            {
                if (ch is '\r')
                    break;
            }
            else // "\r\n"
            {
                if (ch is '\r')
                {
                    var peek = PeekChar(out var next);
                    if (peek is not null)
                        return peek.Value;
                    if (next is '\n')
                    {
                        // same size-budget rule: the '\n' belongs to this
                        // line only while the budget lasts
                        if (size < 0 || read < size)
                        {
                            err = NextChar(out next);
                            if (err is not null)
                                return err.Value;
                            PyTextCodec.AppendCodePoint(builder, next);
                        }
                        break;
                    }
                }
            }
        }
        return PyStrObject.FromString(builder.ToString());
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
            Debug.Assert(_codec is not null);
            if (size is 0)
                return PyStrObject.Empty;
            return ReadTextLine(size);
        }
        else
        {
            using var ms = new MemoryStream();
            int total = 0;
            // the size budget gates every read: readline(n) consumes exactly
            // min(n, line length) bytes, and readline(0) consumes none
            while (size < 0 || total < size)
            {
                int b = _stream.ReadByte();
                if (b < 0)
                    break;
                ms.WriteByte((byte)b);
                total++;
                if (b is '\n')
                    break;
            }
            return PyBytesObject.FromBytes(ms.ToArray());
        }
    }

    // TextIOWrapper.seek(): a whence-0 offset is a cookie from tell() (bit
    // 62 = pending translated '\r'); the resync forms recompute it. The
    // read state rebuilds purely from the byte position, so any cookie is
    // restorable without decoder bookkeeping.
    private PyResult SeekText(long offset, int whence)
    {
        if (!_isReadable)
        {
            // write-side only: positions are the flushed byte offset. The
            // encoder state resets on every seek: the codec preamble is
            // armed only when the final position is the very start
            FlushWriteBuffer();
            var writePos = _stream.Seek(offset, (SeekOrigin)whence);
            _wrotePreamble = writePos is not 0;
            return PyIntObject.FromInteger(writePos);
        }

        long pos;
        bool flag;
        switch (whence)
        {
            case 0:
                pos = offset & ~CookieFlagMask;
                flag = (offset & CookieFlagMask) is not 0;
                break;
            case 1:
                // resync: drop readahead and restart from the next
                // character's offset, keeping the owed '\r' translation
                flag = _pendingCR && _charStart >= _charEnd;
                pos = flag ? _frontPos + _pendingCRLen : _frontPos;
                break;
            default: // 2
                pos = _stream.Length;
                flag = false;
                break;
        }
        ResetTextReadState(pos, flag);
        _stream.Seek(pos, SeekOrigin.Begin);
        _wrotePreamble = pos is not 0;
        return PyIntObject.FromInteger(PackCookie(pos, flag));
    }
}

[AIGenerated]
// module position "_io" like CPython's io stack types (the qual name stays
// bare; ReprName composes it as "_io.TextIOWrapper" from the module)
[PyType("TextIOWrapper", Module = "_io")]
public sealed partial class PyTextIOWrapperObjectType : PyTypeObject<PyTextIOWrapperObject>
{
    protected override PyResult Repr(PyCallContext context, PyTextIOWrapperObject self)
    {
        // CPython's text repr carries the encoding; the binary layers
        // (Buffered*) print no encoding field
        return self._isTextMode
            ? PyStrObject.FromString($"<_io.TextIOWrapper name='{self._name}' mode='{self._mode}' encoding='{self._encodingName}'>")
            : PyStrObject.FromString($"<_io.TextIOWrapper name='{self._name}' mode='{self._mode}'>");
    }

    protected override PyResult Enter(PyCallContext context, PyTextIOWrapperObject self)
    {
        var check = self.CheckClosed();
        if (check.IsError)
            return check;
        return self;
    }

    protected override PyResult Exit(PyCallContext context, PyTextIOWrapperObject self, PyObject excType, PyObject excVal, PyObject excTb)
    {
        return self.Close();
    }

    protected override PyResult Iter(PyCallContext context, PyTextIOWrapperObject self)
    {
        var check = self.CheckClosed();
        if (check.IsError)
            return check;
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyTextIOWrapperObject self)
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
    private static PyResult Read(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
    {
        var sizeObj = arguments[0];
        if (sizeObj is PyNoneObject)
            return self.Read(context);
        var size = IndexOrError(context, sizeObj, out var sizeError);
        if (size is null)
        {
            // read() rejects objects without __index__ with its own message
            if (sizeObj.PyType.Slots.Index is null)
                return PyResult.TypeError(PySR.Runtime_File_SizeIntegerOrNone, sizeObj.PyType.TpName);
            return sizeError;
        }
        var bigSize = size.Value;
        if (bigSize > long.MaxValue || bigSize < long.MinValue)
            return PyResult.OverflowError(PySR.Runtime_Index_CannotFitInt);
        if (bigSize > int.MaxValue || bigSize < int.MinValue)
            return self.Read(context); // beyond C int the limit never binds
        return self.Read(context, size.Int32Value);
    }

    [PyMethod("write")]
    [PyFunctionParameters("data", "/")]
    private static PyResult Write(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
    {
        return self.Write(context, arguments[0]);
    }

    [PyMethod("close")]
    [PyFunctionParameters()]
    private static PyResult Close(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
    {
        return self.Close();
    }

    [PyMethod("flush")]
    [PyFunctionParameters()]
    private static PyResult Flush(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
    {
        return self.Flush();
    }

    [PyMethod("seek")]
    [PyFunctionParameters("offset", "whence=0", "/")]
    private static PyResult Seek(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
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
        if (self._isTextMode)
        {
            if (whenceInt.Int32Value is 0)
            {
                // the negativity check runs on the original value
                if (bigOffset < 0)
                    return PyResult.ValueError(PySR.Runtime_File_NegativeSeekPosition, bigOffset);
                // CPython truncates a text cookie to 64 bits and reports
                // EINVAL only when the truncated value is negative; it still
                // returns the cookie as passed
                var truncated = bigOffset & (BigInteger)ulong.MaxValue;
                if (truncated > long.MaxValue)
                    return PyUtils.OSErrorFromErrno(PyOSErrorObjectType.Shared, 22, PySR.Runtime_Os_StrerrorInvalidArgument);
                var seekResult = self.Seek((long)truncated, 0);
                if (!seekResult.IsError)
                    return PyIntObject.FromInteger(bigOffset);
                return seekResult;
            }
            // cur/end-relative: only the nonzero-ness matters for the
            // UnsupportedOperation check, whatever the magnitude
            if (bigOffset > long.MaxValue || bigOffset < long.MinValue)
                return self.Seek(bigOffset == 0 ? 0 : 1, whenceInt.Int32Value);
            return self.Seek((long)bigOffset, whenceInt.Int32Value);
        }
        if (bigOffset > long.MaxValue || bigOffset < long.MinValue)
            return PyResult.ValueError(PySR.Runtime_File_CannotFitOffset);
        return self.Seek((long)bigOffset, whenceInt.Int32Value);
    }

    internal static PyIntObject? IndexOrError(PyCallContext context, PyObject obj, out PyResult error)
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
    private static PyResult Tell(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
    {
        return self.Tell();
    }

    [PyMethod("readline")]
    [PyFunctionParameters("size=-1", "/")]
    private static PyResult ReadLine(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
    {
        // the text layer's readline rejects None; the binary BufferedReader
        // readline accepts it
        if (arguments[0] is PyNoneObject && !self._isTextMode)
            return self.ReadLine();
        var size = IndexOrError(context, arguments[0], out var sizeError);
        if (size is null)
            return sizeError;
        var bigSize = size.Value;
        if (bigSize > long.MaxValue || bigSize < long.MinValue)
        {
            return self._isTextMode
                ? PyResult.OverflowError(PySR.Runtime_Number_Int_TooLargeForSsize)
                : PyResult.OverflowError(PySR.Runtime_Index_CannotFitInt);
        }
        if (bigSize > int.MaxValue || bigSize < int.MinValue)
            return self.ReadLine(); // beyond C int the limit never binds
        return self.ReadLine(size.Int32Value);
    }

    [PyMethod("readlines")]
    [PyFunctionParameters("hint=-1", "/")]
    private static PyResult ReadLines(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
    {
        // CPython _IOBase.readlines: keep readline() until EOF, stopping
        // once the accumulated size EXCEEDS a positive hint (the crossing
        // line is kept); text mode counts characters (code points), binary
        // mode bytes.
        BigInteger hint = -1;
        var hintObj = arguments[0];
        if (hintObj is not PyNoneObject)
        {
            var hintInt = IndexOrError(context, hintObj, out var hintError);
            if (hintInt is null)
            {
                if (hintObj.PyType.Slots.Index is null)
                    return PyResult.TypeError(PySR.Runtime_File_SizeIntegerOrNone, hintObj.PyType.TpName);
                return hintError;
            }
            if (hintInt.Value > long.MaxValue)
                return PyResult.OverflowError(PySR.Runtime_Index_CannotFitInt);
            hint = hintInt.Value;
        }

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
                PyStrObject s => PyTextIOWrapperObject.CodePointLength(s.Value),
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
    private static PyResult Readable(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
    {
        // CPython raises only when the file closed with the capability on
        var flag = self._isReadable;
        if (self.IsClosed && flag)
            return PyResult.ValueError(PySR.Runtime_File_ClosedNoPeriod);
        return PyBoolObject.FromBoolean(flag);
    }

    [PyMethod("writable")]
    [PyFunctionParameters()]
    private static PyResult Writable(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
    {
        // CPython raises only when the file closed with the capability on
        var flag = self._isWritable;
        if (self.IsClosed && flag)
            return PyResult.ValueError(PySR.Runtime_File_ClosedNoPeriod);
        return PyBoolObject.FromBoolean(flag);
    }

    [PyMethod("seekable")]
    [PyFunctionParameters()]
    private static PyResult Seekable(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
    {
        // CPython raises only when the file closed with the capability on
        var flag = self._isSeekable;
        if (self.IsClosed && flag)
            return PyResult.ValueError(PySR.Runtime_File_ClosedNoPeriod);
        return PyBoolObject.FromBoolean(flag);
    }

    [PyProperty("closed")]
    private static PyResult Get_closed(PyCallContext context, PyTextIOWrapperObject self)
    {
        return PyBoolObject.FromBoolean(self.IsClosed);
    }

    [PyProperty("mode")]
    private static PyResult Get_mode(PyCallContext context, PyTextIOWrapperObject self)
    {
        return PyStrObject.FromString(self._mode);
    }

    [PyProperty("name")]
    private static PyResult Get_name(PyCallContext context, PyTextIOWrapperObject self)
    {
        return PyStrObject.FromString(self._name);
    }

    // encoding/errors live on the text layer only (CPython TextIOWrapper);
    // binary files raise AttributeError like CPython's BufferedReader
    [PyProperty("encoding")]
    private static PyResult Get_encoding(PyCallContext context, PyTextIOWrapperObject self)
    {
        if (!self._isTextMode)
            return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, "_io.TextIOWrapper", "encoding");
        return PyStrObject.FromString(self._encodingName);
    }

    [PyProperty("errors")]
    private static PyResult Get_errors(PyCallContext context, PyTextIOWrapperObject self)
    {
        if (!self._isTextMode)
            return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, "_io.TextIOWrapper", "errors");
        return PyStrObject.FromString(self._errorsName);
    }

    [PyMethod("isatty")]
    [PyFunctionParameters()]
    private static PyResult IsAtty(PyCallContext context, PyTextIOWrapperObject self, PyArguments arguments)
    {
        // IOBase.isatty raises the no-period closed error like the
        // capability queries do
        var check = self.CheckClosedNoPeriod();
        if (check.IsError)
            return check;
        return PyBoolObject.FromBoolean(self._isTerminal);
    }

    // CPython exposes these as get/set attributes (the set path belongs to
    // reconfigure(), which is not wired yet), so they read-only here
    [PyProperty("line_buffering")]
    private static PyResult Get_line_buffering(PyCallContext context, PyTextIOWrapperObject self)
    {
        return PyBoolObject.FromBoolean(self._buffering is PyStandardStreamBuffering.Line);
    }

    [PyProperty("write_through")]
    private static PyResult Get_write_through(PyCallContext context, PyTextIOWrapperObject self)
    {
        return PyBoolObject.FromBoolean(self._buffering is PyStandardStreamBuffering.WriteThrough);
    }
}

using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Text;
using CodecKind = PySharp.Modules.Builtins.PyCodecInfo.CodecKind;

namespace PySharp.Modules.IO;

internal enum PyDecodeStatus
{
    /// <summary>
    /// Progress: <paramref name="index"/> advanced by the consumed byte
    /// count and 0-2 UTF-16 units landed in the builder (0 units when an
    /// ignore-style handler silently consumed the bytes).
    /// </summary>
    Ok,
    /// <summary>The sequence at the end of the span is truncated; more input may complete it.</summary>
    NeedMore,
    /// <summary>Input is exhausted with nothing more to produce (generic codecs only).</summary>
    EndOfInput,
    /// <summary>The decode failed and no handler recovered it; <c>error</c> carries the PyResult.</summary>
    Error,
    /// <summary>Bytes were absorbed into the decoder state without output; the
    /// window pointer is committed and more input is required.</summary>
    Pending,
}

/// <summary>
/// Incremental, byte-exact decoder for the text-mode file layer
/// (Modules/_io/textio.c's incremental decoder). Decodes ONE code point per
/// call from a caller-owned raw buffer so the byte offset of every produced
/// character is known — the bookkeeping TextIOWrapper.tell()'s cookie needs.
///
/// The core codecs (utf-8, ascii, latin-1, utf-16/32 with explicit or
/// sniffed byte order) decode with CPython's exact error events, reasons
/// and subpart handling; every other codec falls back to a BCL Decoder
/// driven one code point at a time (its malformed-sequence subparts may
/// differ from CPython's, which the tell/seek roundtrip is insensitive to).
/// </summary>
[AIGenerated]
internal sealed class PyTextCodec
{
    /// <summary>Codec name used in UnicodeDecodeError messages; sniffed
    /// codecs report their resolved byte order once the BOM is seen.</summary>
    private string _errorName;

    /// <summary>Stream offset of the raw window's first byte, so error
    /// positions survive the buffer's compaction.</summary>
    internal long PositionBase { get; set; }

    /// <summary>The errors= handler name, resolved lazily at the first error like CPython.</summary>
    private readonly string _errors;

    private readonly PyCodecInfo.CodecKind _kind;

    /// <summary>utf-8-sig: a leading EF BB BF is dropped before decoding.</summary>
    internal bool StripUtf8Bom { get; }

    // bare 'utf-16'/'utf-32' sniff a leading BOM for the byte order (CPython
    // _PyUnicode_DecodeUTF16/32); once sniffed the order is decoder state and
    // a seek cookie does not carry it — explicit -le/-be forms are stateless
    private readonly bool _sniffBom;
    private bool _sniffed;
    private bool _bigEndian;
    private Decoder? _genericDecoder;
    private GenericWidthMode _widthMode;

    private PyTextCodec(string errorName, string errors, PyCodecInfo.CodecKind kind,
        bool stripUtf8Bom = false, bool sniffBom = false)
    {
        _errorName = errorName;
        _errors = errors;
        _kind = kind;
        StripUtf8Bom = stripUtf8Bom;
        _sniffBom = sniffBom;
    }

    /// <summary>
    /// Resolves the codec like CPython's codecs.lookup: PySharp's alias table
    /// first (its normalization covers the Python alias spellings); an
    /// unknown name is a LookupError, surfaced through <paramref name="error"/>
    /// at open time.
    /// </summary>
    internal static PyTextCodec Create(string encoding, string errors, out PyResult? error)
    {
        error = null;
        Encoding generic;
        try
        {
            generic = PyStrObjectType.GetEncoding(encoding);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            error = PyResult.LookupError(PySR.Runtime_Codec_UnknownEncoding, encoding);
            return null!;
        }

        var normalized = PyStrObjectType.NormalizeEncodingName(encoding);
        switch (normalized)
        {
            case "utf8":
                return new PyTextCodec("utf-8", errors, CodecKind.Utf8);
            case "utf8sig":
                return new PyTextCodec("utf-8", errors, CodecKind.Utf8, stripUtf8Bom: true);
            case "utf16":
                return new PyTextCodec("utf-16", errors, CodecKind.Utf16, sniffBom: true);
            case "utf16le":
                return new PyTextCodec("utf-16-le", errors, CodecKind.Utf16Le);
            case "utf16be":
                return new PyTextCodec("utf-16-be", errors, CodecKind.Utf16Be);
            case "utf32":
                return new PyTextCodec("utf-32", errors, CodecKind.Utf32, sniffBom: true);
            case "utf32le":
                return new PyTextCodec("utf-32-le", errors, CodecKind.Utf32Le);
            case "utf32be":
                return new PyTextCodec("utf-32-be", errors, CodecKind.Utf32Be);
        }
        if (PyCodecInfo.IsAscii(normalized))
            return new PyTextCodec("ascii", errors, CodecKind.Ascii);
        if (PyCodecInfo.IsLatin1(normalized))
            return new PyTextCodec("iso8859-1", errors, CodecKind.Latin1);

        // a codec outside the exact-decoding set rides a BCL Decoder; the
        // errors= handler resolves lazily at the first decode error exactly
        // like CPython's handler lookup, and every decode-side handler
        // (including the escape styles) translates through the shared
        // ApplyDecodeHandler table. Errors keep the caller's spelling of
        // the codec name.
        return new PyTextCodec(encoding, errors, CodecKind.Other, genericEncoding: generic);
    }

    /// <summary>
    /// Decodes one code point from <c>data[index..]</c>, appending 1-2 UTF-16
    /// units to <paramref name="sb"/>. On Ok, <paramref name="index"/> has
    /// advanced past the consumed bytes; the caller computes the byte count
    /// from the difference (a handler may consume bytes with no output).
    /// NeedMore only returns with <paramref name="eof"/> false; with it set,
    /// a truncated tail becomes a handled error event or an Error.
    /// </summary>
    internal PyDecodeStatus DecodeStep(ReadOnlySpan<byte> data, ref int index, bool eof, StringBuilder sb, out PyResult? error)
    {
        error = null;
        if (_kind is CodecKind.Other)
            return DecodeGeneric(data, ref index, eof, sb, out error);

        bool sniffedNow = false;
        if (_sniffBom && !_sniffed)
        {
            var markLen = _kind is CodecKind.Utf16 ? 2 : 4;
            if (data.Length - index < markLen)
            {
                if (!eof)
                    return PyDecodeStatus.NeedMore;
                // too short to hold a BOM: the tail decodes in the default
                // byte order, like CPython's truncated tail
                _sniffed = true;
                _errorName = _kind is CodecKind.Utf16 ? "utf-16-le" : "utf-32-le";
                sniffedNow = false;
            }
            else
            {
                var askedName = _errorName;
                _sniffed = true;
                sniffedNow = true;
                bool bomSeen = false;
                if (markLen is 2 && data.Length - index >= 2)
                {
                    if (data[index] is 0xFE && data[index + 1] is 0xFF) { _bigEndian = true; index += 2; bomSeen = true; }
                    else if (data[index] is 0xFF && data[index + 1] is 0xFE) { index += 2; bomSeen = true; }
                }
                else if (markLen is 4 && data.Length - index >= 4)
                {
                    if (data[index] is 0x00 && data[index + 1] is 0x00 && data[index + 2] is 0xFE && data[index + 3] is 0xFF) { _bigEndian = true; index += 4; bomSeen = true; }
                    else if (data[index] is 0xFF && data[index + 1] is 0xFE && data[index + 2] is 0x00 && data[index + 3] is 0x00) { index += 4; bomSeen = true; }
                }
                if (!bomSeen)
                {
                    // CPython's stream layer decodes the block in the default
                    // byte order first and only refuses BOM-less input after
                    // that decode came back clean — defer the rejection
                    _pendingBomReject = true;
                    _pendingBomRejectMarkLen = markLen;
                    _pendingBomRejectName = askedName;
                }
                // a BOM was recognized: error events report the resolved
                // byte order, like CPython
                _errorName = _bigEndian
                    ? (markLen is 2 ? "utf-16-be" : "utf-32-be")
                    : (markLen is 2 ? "utf-16-le" : "utf-32-le");
            }
        }

        var status = _kind switch
        {
            CodecKind.Utf8 => DecodeUtf8Step(data, ref index, eof, sb, out error),
            CodecKind.Ascii => DecodeSingleByteStep(data, ref index, sb, out error, ordinalMax: 0x7F, reason: "ordinal not in range(128)"),
            CodecKind.Latin1 => DecodeLatin1Step(data, ref index, sb),
            CodecKind.Utf16 or CodecKind.Utf16Le or CodecKind.Utf16Be => DecodeUtf16Step(data, ref index, eof, sb, out error),
            CodecKind.Utf32 or CodecKind.Utf32Le or CodecKind.Utf32Be => DecodeUtf32Step(data, ref index, eof, sb, out error),
            _ => DecodeGeneric(data, ref index, eof, sb, out error),
        };
        // the BOM skip commits together with the event it precedes: a
        // NeedMore rolls the input pointer back, so the sniff reverts too
        // and the BOM is re-recognized next round instead of being decoded
        // as text
        if (sniffedNow && status is PyDecodeStatus.NeedMore)
        {
            _sniffed = false;
            _bigEndian = false;
        }
        return status;
    }

    // seek back to the start re-recognizes the BOM of sniffed codecs, like
    // utf-8-sig's strip-on-seek-zero; any pending absorbed bytes are dropped
    // with the stale decoder state
    internal void ResetBomSniff()
    {
        _sniffed = false;
        _bigEndian = false;
        _pendingBomReject = false;
    }

    // a seek to a non-zero position skips the BOM region: without this the
    // first decode after the seek would re-sniff whatever bytes live there;
    // the machine byte order stands until the state is reset
    internal void MarkBomSniffedIfUnsniffed()
    {
        if (!_sniffed)
        {
            _sniffed = true;
            _bigEndian = false;
        }
    }

    /// <summary>A BOM-less stream whose first native decode event is still pending.</summary>
    internal bool HasPendingBomReject => _pendingBomReject;

    // a seek makes any decoder-internal state stale
    internal void ResetGenericDecoder() => _genericDecoder = null;

    // BOM-less utf-16/32: set when the sniff found no BOM; the stream layer
    // raises its BOM rejection only after a decode event comes back clean
    private bool _pendingBomReject;
    private int _pendingBomRejectMarkLen;
    private string _pendingBomRejectName = "";

    /// <summary>
    /// Consumes a deferred BOM rejection after a clean decode event,
    /// reporting it over the first <paramref name="markLen"/> stream bytes.
    /// </summary>
    internal PyResult? TakePendingBomReject(ReadOnlySpan<byte> data, int index)
    {
        if (!_pendingBomReject)
            return null;
        _pendingBomReject = false;
        return PyBytesObjectType.CreateDecodeError(_pendingBomRejectName, data,
            Math.Max(0, index - _pendingBomRejectMarkLen), index,
            PySR.Runtime_Codec_StreamNoBom, PositionBase);
    }

    private PyDecodeStatus DecodeUtf8Step(ReadOnlySpan<byte> data, ref int index, bool eof, StringBuilder sb, out PyResult? error)
    {
        error = null;
        int start = index;
        int code = PyBytesObjectType.Utf8DecodeStep(data, ref index, sb);
        if (code is 0)
            return PyDecodeStatus.Ok;
        if (code < 0)
        {
            if (!eof)
            {
                index = start;
                return PyDecodeStatus.NeedMore;
            }
            return ApplyHandler(data, start, data.Length, "unexpected end of data", sb, out index, out error)
                ? PyDecodeStatus.Ok
                : PyDecodeStatus.Error;
        }
        var end = start + (code is 1 ? 1 : code - 1);
        var reason = code is 1 ? "invalid start byte" : "invalid continuation byte";
        return ApplyHandler(data, start, end, reason, sb, out index, out error)
            ? PyDecodeStatus.Ok
            : PyDecodeStatus.Error;
    }

    private PyDecodeStatus DecodeSingleByteStep(ReadOnlySpan<byte> data, ref int index, StringBuilder sb,
        out PyResult? error, int ordinalMax, string reason)
    {
        error = null;
        int b = data[index];
        if (b <= ordinalMax)
        {
            sb.Append((char)b);
            index++;
            return PyDecodeStatus.Ok;
        }
        return ApplyHandler(data, index, index + 1, reason, sb, out index, out error)
            ? PyDecodeStatus.Ok
            : PyDecodeStatus.Error;
    }

    private PyDecodeStatus DecodeLatin1Step(ReadOnlySpan<byte> data, ref int index, StringBuilder sb)
    {
        // latin-1 maps every byte, it can never fail
        sb.Append((char)data[index]);
        index++;
        return PyDecodeStatus.Ok;
    }

    private PyDecodeStatus DecodeUtf16Step(ReadOnlySpan<byte> data, ref int index, bool eof, StringBuilder sb, out PyResult? error)
    {
        error = null;
        bool bigEndian = _kind is CodecKind.Utf16Be || (_kind is CodecKind.Utf16 && _bigEndian);
        int start = index;
        // nothing after the BOM is a clean end, not a truncated unit
        if (data.Length - index is 0)
            return eof ? PyDecodeStatus.EndOfInput : PyDecodeStatus.NeedMore;
        if (data.Length - index < 2)
            return Truncated(data, ref index, start, eof, sb, out error);
        int unit = bigEndian ? (data[index] << 8) | data[index + 1] : (data[index + 1] << 8) | data[index];
        index += 2;
        if (unit is >= 0xD800 and <= 0xDBFF)
        {
            if (data.Length - index < 2)
            {
                // surrogatepass emits a truncated high surrogate at
                // end-of-input, like CPython; mid-stream it must wait for
                // the low half like every other handler
                if (_errors is "surrogatepass" && eof)
                {
                    sb.Append((char)unit);
                    return PyDecodeStatus.Ok;
                }
                return Truncated(data, ref index, start, eof, sb, out error, "unexpected end of data");
            }
            int low = bigEndian ? (data[index] << 8) | data[index + 1] : (data[index + 1] << 8) | data[index];
            if (low is >= 0xDC00 and <= 0xDFFF)
            {
                index += 2;
                sb.Append((char)unit);
                sb.Append((char)low);
                return PyDecodeStatus.Ok;
            }
            // high surrogate followed by a non-low unit: error the 2-byte
            // high unit, the non-low unit redecodes from the resume point
        }
        else if (unit is < 0xDC00 or > 0xDFFF)
        {
            sb.Append((char)unit);
            return PyDecodeStatus.Ok;
        }
        // surrogatepass lets an isolated surrogate unit through, like
        // CPython's utf-16 codecs
        if (_errors is "surrogatepass")
        {
            sb.Append((char)unit);
            return PyDecodeStatus.Ok;
        }
        string surrogateReason = unit is >= 0xD800 and <= 0xDBFF
            ? "illegal UTF-16 surrogate"
            : "illegal encoding";
        return ApplyHandler(data, start, start + 2, surrogateReason, sb, out index, out error)
            ? PyDecodeStatus.Ok
            : PyDecodeStatus.Error;
    }

    private PyDecodeStatus DecodeUtf32Step(ReadOnlySpan<byte> data, ref int index, bool eof, StringBuilder sb, out PyResult? error)
    {
        error = null;
        bool bigEndian = _kind is CodecKind.Utf32Be || (_kind is CodecKind.Utf32 && _bigEndian);
        int start = index;
        // nothing after the BOM is a clean end, not a truncated unit
        if (data.Length - index is 0)
            return eof ? PyDecodeStatus.EndOfInput : PyDecodeStatus.NeedMore;
        if (data.Length - index < 4)
            return Truncated(data, ref index, start, eof, sb, out error);
        long word = bigEndian
            ? ((long)data[index] << 24) | ((long)data[index + 1] << 16) | ((long)data[index + 2] << 8) | data[index + 3]
            : ((long)data[index + 3] << 24) | ((long)data[index + 2] << 16) | ((long)data[index + 1] << 8) | data[index];
        index += 4;
        string reason;
        if (word is >= 0xD800 and <= 0xDFFF)
        {
            // surrogatepass lets an isolated surrogate code point through,
            // like CPython's utf-32 codecs
            if (_errors is "surrogatepass")
            {
                sb.Append((char)word);
                return PyDecodeStatus.Ok;
            }
            reason = "code point in surrogate code point range(0xd800, 0xe000)";
        }
        else if (word > 0x10FFFF)
            reason = "code point not in range(0x110000)";
        else
        {
            AppendCodePoint(sb, (int)word);
            return PyDecodeStatus.Ok;
        }
        return ApplyHandler(data, start, start + 4, reason, sb, out index, out error)
            ? PyDecodeStatus.Ok
            : PyDecodeStatus.Error;
    }

    // a truncated tail needs more bytes mid-stream; at EOF it becomes the
    // codec's error event (an odd unit tail is "truncated data", a missing
    // low surrogate "unexpected end of data")
    private PyDecodeStatus Truncated(ReadOnlySpan<byte> data, ref int index, int start, bool eof, StringBuilder sb, out PyResult? error, string reason = "truncated data")
    {
        if (!eof)
        {
            index = start;
            error = null;
            return PyDecodeStatus.NeedMore;
        }
        return ApplyHandler(data, start, data.Length, reason, sb, out index, out error)
            ? PyDecodeStatus.Ok
            : PyDecodeStatus.Error;
    }

    private bool ApplyHandler(ReadOnlySpan<byte> data, int start, int end, string reason, StringBuilder sb, out int next, out PyResult? error)
    {
        var source = PyBytesObject.FromBytes(data.ToArray());
        // surrogatepass only rewrites utf-8's encoded-surrogate pattern; on
        // every other codec its errors stand, like CPython
        var errors = _errors is "surrogatepass" && _kind is not CodecKind.Utf8 ? "strict" : _errors;
        return PyBytesObjectType.ApplyDecodeHandler(data, start, end, reason,
            errors, _errorName, source, sb, out next, out error, PositionBase);
    }

    internal static void AppendCodePoint(StringBuilder sb, int codePoint)
    {
        if (codePoint < 0x10000)
        {
            sb.Append((char)codePoint);
        }
        else
        {
            sb.Append((char)(0xD800 + ((codePoint - 0x10000) >> 10)));
            sb.Append((char)(0xDC00 + ((codePoint - 0x10000) & 0x3FF)));
        }
    }

    private enum GenericWidthMode
    {
        Legacy,     // unknown family: whole-batch Convert (contract caveats documented)
        SingleByte,
        Dbcs,
        EucJp,
        Gb18030,
    }

    private GenericWidthMode ResolveWidthMode()
    {
        if (_genericEncoding.IsSingleByte)
            return GenericWidthMode.SingleByte;
        return _genericEncoding.CodePage switch
        {
            54936 => GenericWidthMode.Gb18030,
            932 or 936 or 949 or 950 or 1361 => GenericWidthMode.Dbcs,
            51932 => GenericWidthMode.EucJp,
            51936 or 51949 or 51950 => GenericWidthMode.Dbcs,
            _ => GenericWidthMode.Legacy,
        };
    }

    // the byte width of the code point starting at index, for the known BCL
    // families; predicting it lets Convert see exactly one complete sequence
    // per call, which keeps the one-code-point contract and exact byte counts
    private int GenericCodePointWidth(ReadOnlySpan<byte> data, int index)
    {
        int b0 = data[index];
        switch (_widthMode)
        {
            case GenericWidthMode.SingleByte:
                return 1;
            case GenericWidthMode.Dbcs:
                // cp932: the halfwidth katakana block is single-byte
                if (_genericEncoding.CodePage == 932 && b0 is >= 0xA1 and <= 0xDF)
                    return 1;
                return b0 >= 0x80 ? 2 : 1;
            case GenericWidthMode.EucJp:
                if (b0 == 0x8F) return 3;
                if (b0 == 0x8E) return 2;
                return b0 >= 0xA1 ? 2 : 1;
            case GenericWidthMode.Gb18030:
                if (b0 < 0x80) return 1;
                if (index + 1 < data.Length && data[index + 1] is >= 0x30 and <= 0x39) return 4;
                return 2;
            default:
                return 1;
        }
    }

    // the BCL fallback: exactly one complete code point per Convert call for
    // the known width families. The errors= handler name only resolves when
    // a fallback exception actually fires, so valid data never rejects an
    // unknown name.
    private PyDecodeStatus DecodeGeneric(ReadOnlySpan<byte> data, ref int index, bool eof, StringBuilder sb, out PyResult? error)
    {
        error = null;
        if (_widthMode == GenericWidthMode.Legacy)
            return DecodeGenericLegacy(data, ref index, eof, sb, out error);

        _genericDecoder ??= PyStrObjectType.MakeStrictDecoder(_genericEncoding);
        if (data.Length - index < 1)
            return eof ? PyDecodeStatus.EndOfInput : PyDecodeStatus.NeedMore;
        int width = GenericCodePointWidth(data, index);
        int sliceLen = Math.Min(width, data.Length - index);
        if (!eof && sliceLen < width)
            return PyDecodeStatus.NeedMore; // wait for the whole code point
        int start = index;
        Span<char> unit = stackalloc char[2];
        int bytesUsed;
        int charsUsed;
        try
        {
            _genericDecoder.Convert(data[index..(index + sliceLen)], unit, flush: eof, out bytesUsed, out charsUsed, out _);
        }
        catch (DecoderFallbackException)
        {
            _genericDecoder = null; // resume with a fresh decoder
            index += sliceLen;
            var reason = eof && sliceLen < width ? "incomplete multibyte sequence"
                : PyCodecInfo.Classify(_errorName).Reason;
            return ApplyHandler(data, start, index, reason, sb, out index, out error)
                ? PyDecodeStatus.Ok
                : PyDecodeStatus.Error;
        }
        if (charsUsed == 0 && bytesUsed > 0 && !eof)
        {
            // a prediction miss left an absorbed partial sequence: the
            // decoder state carries it, commit the real consumption
            index += bytesUsed;
            return PyDecodeStatus.Pending;
        }
        if (charsUsed == 0)
        {
            // end-of-input left less than a full sequence: the whole slice is
            // the incomplete event, never a silent loss
            index += bytesUsed > 0 ? bytesUsed : sliceLen;
            return ApplyHandler(data, start, index, "incomplete multibyte sequence", sb, out index, out error)
                ? PyDecodeStatus.Ok
                : PyDecodeStatus.Error;
        }
        index += sliceLen;
        sb.Append(unit[..charsUsed]);
        return PyDecodeStatus.Ok;
    }

    // unknown BCL families (stateful iso-2022-style codecs): whole-batch
    // Convert; a batch may carry two characters, whose byte counts the
    // caller attributes to the first slot
    private PyDecodeStatus DecodeGenericLegacy(ReadOnlySpan<byte> data, ref int index, bool eof, StringBuilder sb, out PyResult? error)
    {
        error = null;
        _genericDecoder ??= PyStrObjectType.MakeStrictDecoder(_genericEncoding);
        Span<char> unit = stackalloc char[2];
        int bytesUsed;
        int charsUsed;
        try
        {
            _genericDecoder.Convert(data[index..], unit, flush: eof, out bytesUsed, out charsUsed, out _);
        }
        catch (DecoderFallbackException ex)
        {
            _genericDecoder = null; // resume with a fresh decoder
            int badStart = ex.Index < 0 ? index : index + ex.Index;
            int badLen = Math.Max(1, (ex.BytesUnknown ?? []).Length);
            var reason = eof ? "incomplete multibyte sequence" : PyCodecInfo.Classify(_errorName).Reason;
            return ApplyHandler(data, badStart, Math.Min(badStart + badLen, data.Length),
                reason, sb, out index, out error)
                ? PyDecodeStatus.Ok
                : PyDecodeStatus.Error;
        }
        if (charsUsed == 0 && bytesUsed == 0)
            return eof ? PyDecodeStatus.EndOfInput : PyDecodeStatus.NeedMore;
        if (charsUsed == 0 && bytesUsed > 0)
        {
            // an absorbed partial sequence; at end-of-input it is the
            // incomplete event, never a silent loss
            if (!eof)
            {
                index += bytesUsed;
                return PyDecodeStatus.Pending;
            }
            index += bytesUsed;
            return ApplyHandler(data, index - bytesUsed, index, "incomplete multibyte sequence", sb, out index, out error)
                ? PyDecodeStatus.Ok
                : PyDecodeStatus.Error;
        }
        index += bytesUsed;
        sb.Append(unit[..charsUsed]);
        return PyDecodeStatus.Ok;
    }

    private readonly Encoding _genericEncoding;

    private PyTextCodec(string errorName, string errors, PyCodecInfo.CodecKind kind, Encoding genericEncoding)
        : this(errorName, errors, kind)
    {
        _genericEncoding = genericEncoding;
        _widthMode = ResolveWidthMode();
    }
}

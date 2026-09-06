using PySharp.Compilation.CodeAnalysis;
using PySharp.Runtime.Calls;
using PySharp.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.Compilation;

// PEP 263 source decoding, mirroring CPython's tokenizer pipeline
// (Parser/tokenizer/helpers.c and file_tokenizer.c): UTF-8 BOM detection,
// a coding-cookie scan over the first two comment-or-blank lines, strict
// decoding with the declared codec, and codec-name validation. Invalid
// bytes are never silently replaced: they raise SyntaxError like CPython.
[AIGenerated]
internal static class PySourceDecoder
{
    public static string Decode(PyCallContext context, ReadOnlySpan<byte> bytes, string? filename)
    {
        CodePagesEncoding.EnsureRegistered();

        var hasBom = bytes is [0xEF, 0xBB, 0xBF, ..];
        var body = hasBom ? bytes[3..] : bytes;

        var declared = FindDeclaredEncoding(body);
        if (declared is not null && hasBom && declared is not "utf-8")
        {
            throw context.SyntaxError(
                ICodeMetaInfoProvider.Empty, PySR.InvalidSyntax_Tokenize_EncodingProblemWithBom, declared);
        }

        if (declared is null or "utf-8")
        {
            var noEncodingDeclared = declared is null;
            return DecodeUtf8(context, body, filename, noEncodingDeclared);
        }

        if (!TryResolveEncoding(declared, out var encoding))
        {
            throw context.SyntaxError(
                ICodeMetaInfoProvider.Empty, PySR.InvalidSyntax_Tokenize_EncodingProblem, declared);
        }

        var strict = (Encoding)encoding.Clone();
        strict.DecoderFallback = DecoderFallback.ExceptionFallback;
        try
        {
            return strict.GetString(body);
        }
        catch (DecoderFallbackException)
        {
            // CPython recodes through the io layer, where any bad byte in
            // the first read chunk fails the codec setup itself
            throw context.SyntaxError(
                ICodeMetaInfoProvider.Empty, PySR.InvalidSyntax_Tokenize_EncodingProblem, declared);
        }
    }

    private static string DecodeUtf8(PyCallContext context, ReadOnlySpan<byte> bytes, string? filename, bool noEncodingDeclared)
    {
        var index = 0;
        while (index < bytes.Length)
        {
            var length = ValidUtf8Sequence(bytes[index..], out var reason);
            if (length > 0)
            {
                index += length;
                continue;
            }

            var line = 1 + bytes[..index].Count((byte)'\n');
            if (noEncodingDeclared)
            {
                var inFile = filename is null ? string.Empty : $" in file {filename}";
                throw context.SyntaxError(
                    ICodeMetaInfoProvider.Empty, PySR.InvalidSyntax_Tokenize_NonUtf8Code, bytes[index], inFile, line);
            }

            var lineStart = bytes[..index].LastIndexOf((byte)'\n') + 1;
            throw context.SyntaxError(
                ICodeMetaInfoProvider.Empty, PySR.InvalidSyntax_Tokenize_SourceUnicodeError,
                "utf-8", bytes[index], index - lineStart, reason);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    // checks the sequence against CPython's valid_utf8 rejection rules
    // (stray/overlong leads, surrogates, out-of-range planes); the caller
    // reports failures at the lead byte, like CPython's decoder
    private static int ValidUtf8Sequence(ReadOnlySpan<byte> s, out string reason)
    {
        var b = s[0];
        int expected;
        if (b < 0x80)
        {
            reason = null!;
            return 1;
        }
        if (b < 0xC2)
        {
            reason = InvalidStartByte;
            return 0;
        }
        if (b < 0xE0)
        {
            expected = 1;
        }
        else if (b < 0xF0)
        {
            // 0xE0 with a low second byte is overlong, 0xED with a high
            // one decodes into the surrogate range
            if ((b is 0xE0 && SecondByteIs(s, static c => c < 0xA0)) ||
                (b is 0xED && SecondByteIs(s, static c => c >= 0xA0)))
            {
                reason = InvalidStartByte;
                return 0;
            }
            expected = 2;
        }
        else if (b < 0xF5)
        {
            // 0xF0 with a low second byte is overlong, 0xF4 with a high
            // one overflows the Unicode range
            if ((b is 0xF0 && SecondByteIs(s, static c => c < 0x90)) ||
                (b is 0xF4 && SecondByteIs(s, static c => c >= 0x90)))
            {
                reason = InvalidStartByte;
                return 0;
            }
            expected = 3;
        }
        else
        {
            reason = InvalidStartByte;
            return 0;
        }

        for (var i = 1; i <= expected; i++)
        {
            // the tokenizer fakes a trailing newline, so a truncated tail
            // fails its lead byte like any other bad continuation
            if (i >= s.Length || s[i] < 0x80 || s[i] >= 0xC0)
            {
                reason = InvalidContinuationByte;
                return 0;
            }
        }

        reason = null!;
        return expected + 1;
    }

    private static bool SecondByteIs(ReadOnlySpan<byte> s, Func<byte, bool> predicate)
        => s.Length >= 2 && predicate(s[1]);

    private const string InvalidStartByte = "invalid start byte";
    private const string InvalidContinuationByte = "invalid continuation byte";

    // scans the first two lines like CPython's check_coding_spec driver:
    // a cookie is only read while every earlier line held nothing but
    // whitespace up to a comment
    private static string? FindDeclaredEncoding(ReadOnlySpan<byte> body)
    {
        var lineStart = 0;
        for (var lineNumber = 0; lineNumber < 2; lineNumber++)
        {
            var newline = body[lineStart..].IndexOf((byte)'\n');
            var line = newline is -1 ? body[lineStart..] : body[lineStart..(lineStart + newline)];

            if (GetCodingSpec(line) is { } declared)
                return declared;

            foreach (var b in line)
            {
                if (b is (byte)'\r' or (byte)'#')
                    break;
                if (b is not ((byte)' ' or (byte)'\t' or 0x0C))
                    return null;
            }

            if (newline is -1)
                break;
            lineStart += newline + 1;
        }

        return null;
    }

    // get_coding_spec: the cookie must sit inside a comment, matched as
    // `coding` + ':'/'=' + optional spaces + an alphanumeric/dash/underscore/
    // dot run; the search runs from the '#' onward (so "# encoding: gbk"
    // also matches)
    private static string? GetCodingSpec(ReadOnlySpan<byte> line)
    {
        var i = 0;
        for (; i < line.Length - 6; i++)
        {
            if (line[i] is (byte)'#')
                break;
            if (line[i] is not ((byte)' ' or (byte)'\t' or 0x0C))
                return null;
        }

        for (; i < line.Length - 6; i++)
        {
            if (!line.Slice(i, 6).SequenceEqual("coding"u8))
                continue;

            var t = i + 6;
            if (line[t] is not ((byte)':' or (byte)'='))
                continue;
            t++;
            while (t < line.Length && line[t] is (byte)' ' or (byte)'\t')
                t++;

            var begin = t;
            while (t < line.Length && (char.IsAsciiLetterOrDigit((char)line[t]) || line[t] is (byte)'-' or (byte)'_' or (byte)'.'))
                t++;

            if (begin < t)
                return NormalizeEncodingName(line[begin..t]);
        }

        return null;
    }

    // get_normal_name: lowercase with '_' mapped to '-' over the first 12
    // characters, collapsing the utf-8 and latin-1 spelling families;
    // anything else keeps its original spelling
    private static string NormalizeEncodingName(ReadOnlySpan<byte> name)
    {
        Span<char> normalized = stackalloc char[12];
        var length = Math.Min(name.Length, normalized.Length);
        for (var i = 0; i < length; i++)
        {
            var c = (char)name[i];
            normalized[i] = c is '_' ? '-' : char.ToLowerInvariant(c);
        }

        var prefix = new string(normalized[..length]);
        if (prefix is "utf-8" || prefix.StartsWith("utf-8-"))
            return "utf-8";
        if (prefix is "latin-1" or "iso-8859-1" or "iso-latin-1" ||
            prefix.StartsWith("latin-1-") || prefix.StartsWith("iso-8859-1-") || prefix.StartsWith("iso-latin-1-"))
            return "iso-8859-1";

        return Encoding.ASCII.GetString(name);
    }

    // resolves a Python codec name against the .NET tables; only the
    // dashed utf-16 spellings are unknown to Encoding.GetEncoding
    private static bool TryResolveEncoding(string name, [NotNullWhen(true)] out Encoding? encoding)
    {
        switch (name)
        {
            case "utf-16-le":
                encoding = Encoding.Unicode;
                return true;
            case "utf-16-be":
                encoding = Encoding.BigEndianUnicode;
                return true;
        }

        try
        {
            encoding = Encoding.GetEncoding(name);
            return true;
        }
        catch (ArgumentException)
        {
        }

        try
        {
            encoding = Encoding.GetEncoding(name.Replace('_', '-'));
            return true;
        }
        catch (ArgumentException)
        {
        }

        encoding = null;
        return false;
    }
}

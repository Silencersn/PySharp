using PySharp.Compilation.CodeAnalysis;
using System.Text;

namespace PySharp.Compilation.Bytecodes;

internal sealed class LineTable
{
    private const byte RangeFlag = 0b0001;
    private const byte CrucialRangeFlag = 0b0010;
    private const byte NegativeRangeStartDiffFlag = 0b0100;

    private readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;
    private readonly BinaryReader _reader;

    private CodeSource? _source;

    private int _indexToWrite;
    private CodeMetaInfo? _infoToWrite;

    private int _lastIndex;
    private int _lastNonEmptyRangeStart;

    internal LineTable()
    {
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
        _reader = new BinaryReader(_stream, Encoding.ASCII, leaveOpen: true);

        _indexToWrite = -1;
        _infoToWrite = null;
    }

    public void Write(int index, CodeMetaInfo? info)
    {
        _source ??= info?.Source;

        if (_indexToWrite is not -1 && _indexToWrite != index)
            InternalWrite(_indexToWrite, _infoToWrite);

        _indexToWrite = index;
        _infoToWrite = info;
    }

    public void EnsureWritten()
    {
        if (_indexToWrite is -1)
            return;

        InternalWrite(_indexToWrite, _infoToWrite);
        _indexToWrite = -1;
        _infoToWrite = null;
    }

    private void InternalWrite(int index, CodeMetaInfo? info)
    {
        _writer.Write7BitEncodedInt(index - _lastIndex);
        _lastIndex = index;
        var range = CodeTextSpan.Empty;
        var crucialRange = CodeTextSpan.Empty;
        if (info is not null)
        {
            range = info.Range;
            if (!range.IsEmpty)
                crucialRange = info.CrucialRange;
        }

        if (range.IsEmpty)
        {
            _writer.Write(default(byte));
            return;
        }

        var flag = RangeFlag;
        if (!crucialRange.IsEmpty)
            flag |= CrucialRangeFlag;

        var rangeStartDiff = range.Start - _lastNonEmptyRangeStart;
        if (rangeStartDiff < 0)
        {
            rangeStartDiff = -rangeStartDiff;
            flag |= NegativeRangeStartDiffFlag;
        }

        _writer.Write(flag);
        _writer.Write7BitEncodedInt(rangeStartDiff);
        _writer.Write7BitEncodedInt(range.Length);
        _lastNonEmptyRangeStart = range.Start;

        if (!crucialRange.IsEmpty)
        {
            _writer.Write7BitEncodedInt(crucialRange.Start - range.Start);
            _writer.Write7BitEncodedInt(range.Length - crucialRange.Length);
        }
    }

    public CodeMetaInfo? Read(int index)
    {
        // TODO: thread-safe

        if (_source is null || index < 0)
            return null;
        
        _stream.Position = 0;
        var length = _stream.Length;

        var currentIndex = 0;
        var currentRangeStart = 0;

        var range = CodeTextSpan.Empty;
        var crucialRange = CodeTextSpan.Empty;

        while (_stream.Position < length)
        {
            var indexDiff = _reader.Read7BitEncodedInt();
            var nextIndex = currentIndex + indexDiff;
            if (index >= currentIndex && index < nextIndex)
                return CodeMetaInfo.FromSpan(_source, range, crucialRange);
            currentIndex = nextIndex;

            var flag = _reader.ReadByte();

            if ((flag & RangeFlag) is 0)
            {
                range = CodeTextSpan.Empty;
                crucialRange = CodeTextSpan.Empty;
                continue;
            }

            var rangeStartDiff = _reader.Read7BitEncodedInt();
            if ((flag & NegativeRangeStartDiffFlag) is not 0)
                rangeStartDiff = -rangeStartDiff;
            currentRangeStart += rangeStartDiff;
            var rangeLength = _reader.Read7BitEncodedInt();
            range = new CodeTextSpan(currentRangeStart, rangeLength);

            if ((flag & CrucialRangeFlag) is 0)
            {
                crucialRange = CodeTextSpan.Empty;
                continue;
            }

            var crucialRangeStart = currentRangeStart + _reader.Read7BitEncodedInt();
            var crucialLength = rangeLength - _reader.Read7BitEncodedInt();
            crucialRange = new CodeTextSpan(crucialRangeStart, crucialLength);
        }

        if (index >= currentIndex && !range.IsEmpty)
            return CodeMetaInfo.FromSpan(_source, range, crucialRange);

        return null;
    }
}

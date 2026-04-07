using PySharp.Compilation.CodeAnalysis;
using System.Diagnostics;
using System.Text;

namespace PySharp.Compilation.Bytecodes;

internal sealed class LineTableBuilder
{
    internal readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;

    private readonly CodeSource _source;

    private int _indexToWrite;
    private ValueCodeMetaInfo _infoToWrite;

    private int _lastIndex;
    private int _lastNonEmptyRangeStart;

    internal LineTableBuilder(CodeSource source)
    {
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);

        _indexToWrite = -1;
        _infoToWrite = ValueCodeMetaInfo.Empty;
        _source = source;
    }

    public LineTable ToLineTable()
    {
        EnsureWritten();
        var lineTable = new LineTable(_source, _stream.GetBuffer(), (int)_stream.Length);
        _stream.Dispose();
        _writer.Dispose();
        return lineTable;
    }

    public void Write(int index, ValueCodeMetaInfo info)
    {
        Debug.Assert(index >= _indexToWrite);

        if (_infoToWrite == info)
            return;

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
        _infoToWrite = ValueCodeMetaInfo.Empty;
    }

    private void InternalWrite(int index, ValueCodeMetaInfo info)
    {
        var range = CodeTextSpan.Empty;
        var crucialRange = CodeTextSpan.Empty;

        if (!info.IsEmpty)
        {
            range = info.Range;
            if (!range.IsEmpty)
                crucialRange = info.CrucialRange;
        }

        byte flag = 0;
        if (!range.IsEmpty)
            flag |= LineTable.HasRangeFlag;
        if (!crucialRange.IsEmpty)
            flag |= LineTable.HasCrucialRangeFlag;

        int indexDiff = index - _lastIndex;
        _lastIndex = index;

        if (indexDiff < 63)
        {
            _writer.Write((byte)((indexDiff << 2) | flag));
        }
        else
        {
            _writer.Write((byte)((63 << 2) | flag));
            _writer.Write7BitEncodedInt(indexDiff - 63);
        }

        if (range.IsEmpty)
            return;

        int rangeStartDiff = range.Start - _lastNonEmptyRangeStart;
        _lastNonEmptyRangeStart = range.Start;

        int zigzagRangeStartDiff = (rangeStartDiff << 1) ^ (rangeStartDiff >> 31);
        _writer.Write7BitEncodedInt(zigzagRangeStartDiff);
        _writer.Write7BitEncodedInt(range.Length);

        if (!crucialRange.IsEmpty)
        {
            _writer.Write7BitEncodedInt(crucialRange.Start - range.Start);
            _writer.Write7BitEncodedInt(range.Length - crucialRange.Length);
        }
    }
}

internal sealed class LineTable
{
    internal const byte HasRangeFlag = 0b01;
    internal const byte HasCrucialRangeFlag = 0b10;

    private readonly CodeSource _source;
    private byte[] _bytes;
    private readonly int _length;

    public LineTable(CodeSource source, byte[] bytes, int length)
    {
        _source = source;
        _bytes = bytes;
        _length = length;
    }

    public CodeMetaInfo? Read(int index)
    {
        if (_source is null || index < 0)
            return null;

        var position = 0;
        var currentIndex = 0;
        var currentRangeStart = 0;

        var range = CodeTextSpan.Empty;
        var crucialRange = CodeTextSpan.Empty;

        while (position < _length)
        {
            var firstByte = ReadByte(_bytes, ref position);
            var flag = firstByte & 0b11;
            var indexDiff = firstByte >> 2;

            if (indexDiff is 63)
                indexDiff += Read7BitEncodedInt(_bytes, ref position);

            var nextIndex = currentIndex + indexDiff;

            if (index >= currentIndex && index < nextIndex)
                return CodeMetaInfo.FromSpan(_source, range, crucialRange);

            currentIndex = nextIndex;

            if ((flag & HasRangeFlag) is 0)
            {
                range = CodeTextSpan.Empty;
                crucialRange = CodeTextSpan.Empty;
                continue;
            }

            int zigzag = Read7BitEncodedInt(_bytes, ref position);
            int rangeStartDiff = (zigzag >> 1) ^ -(zigzag & 1);

            currentRangeStart += rangeStartDiff;
            var rangeLength = Read7BitEncodedInt(_bytes, ref position);
            range = new CodeTextSpan(currentRangeStart, rangeLength);

            if ((flag & HasCrucialRangeFlag) is 0)
            {
                crucialRange = CodeTextSpan.Empty;
                continue;
            }

            var crucialRangeStart = currentRangeStart + Read7BitEncodedInt(_bytes, ref position);
            var crucialLength = rangeLength - Read7BitEncodedInt(_bytes, ref position);
            crucialRange = new CodeTextSpan(crucialRangeStart, crucialLength);
        }

        if (index >= currentIndex && !range.IsEmpty)
            return CodeMetaInfo.FromSpan(_source, range, crucialRange);

        return null;
    }

    public void TrimExcess()
    {
        var threshold = _bytes.Length * 0.9;
        if (_length < threshold)
            _bytes = _bytes[.._length];
    }

    private static byte ReadByte(ReadOnlySpan<byte> bytes, ref int position)
    {
        return bytes[position++];
    }

    private static int Read7BitEncodedInt(ReadOnlySpan<byte> bytes, ref int position)
    {
        uint result = 0;
        byte byteReadJustNow;

        const int MaxBytesWithoutOverflow = 4;
        for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
        {
            byteReadJustNow = ReadByte(bytes, ref position);
            result |= (byteReadJustNow & 0x7Fu) << shift;

            if (byteReadJustNow <= 0x7Fu)
                return (int)result;
        }

        byteReadJustNow = ReadByte(bytes, ref position);
        Debug.Assert(byteReadJustNow <= 0b_1111u);

        result |= (uint)byteReadJustNow << (MaxBytesWithoutOverflow * 7);
        return (int)result;
    }
}

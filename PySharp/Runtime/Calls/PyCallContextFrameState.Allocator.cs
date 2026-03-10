using PySharp.Modules.Builtins;
using System.Buffers;
using System.Diagnostics;

namespace PySharp.Runtime.Calls;

partial class PyCallContextFrameState
{
    internal sealed class PyObjectMemoryAllocator : IDisposable
    {
        internal const int DataChunkSize = 512;

        private PyObject?[][] _chunks;
        private int _chunkCount;

        private struct AllocRecord
        {
            public int Chunk;
            public int Index;
            public int Size;
        }

        private AllocRecord[] _records;
        private int _recordCount;

        private AllocRecord _currentAllocRecord;

        public PyObjectMemoryAllocator()
        {
            _chunks = new PyObject?[4][];
            _records = new AllocRecord[16];

            _currentAllocRecord = new AllocRecord { Chunk = -1, Index = 0, Size = 0 };
        }

        public Memory<PyObject?> Alloc(int size)
        {
            Debug.Assert(size > 0);
            Debug.Assert(size <= DataChunkSize);

            int targetChunk = _currentAllocRecord.Chunk;
            int nextIndex = _currentAllocRecord.Index + _currentAllocRecord.Size;

            if (targetChunk == -1 || nextIndex + size > DataChunkSize)
            {
                targetChunk++;
                nextIndex = 0;

                if (targetChunk == _chunkCount)
                {
                    if (_chunkCount == _chunks.Length)
                        Array.Resize(ref _chunks, _chunks.Length * 2);
                    _chunks[_chunkCount++] = ArrayPool<PyObject?>.Shared.Rent(DataChunkSize);
                }
            }

            if (_recordCount == _records.Length)
                Array.Resize(ref _records, _records.Length * 2);
            _records[_recordCount++] = _currentAllocRecord;

            _currentAllocRecord = new AllocRecord
            {
                Chunk = targetChunk,
                Index = nextIndex,
                Size = size
            };

            return new Memory<PyObject?>(_chunks[targetChunk], nextIndex, size);
        }

        public void Free(Memory<PyObject?> memory)
        {
            Debug.Assert(memory.Length == _currentAllocRecord.Size);

            memory.Span.Clear();
            _currentAllocRecord = _records[--_recordCount];
        }

        public void Dispose()
        {
            for (int i = 0; i < _chunkCount; i++)
            {
                ArrayPool<PyObject?>.Shared.Return(_chunks[i], clearArray: true);
                _chunks[i] = null!;
            }
            _chunkCount = 0;
            _recordCount = 0;
        }
    }
}
using PySharp.Modules.Builtins;
using PySharp.Utility;
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

        private struct FreePtr
        {
            public int Chunk;
            public int Index;
        }

        private FreePtr[] _previousFreePtrs;
        private int _previousFreePtrsCount;

        private FreePtr _freePtr;

        public PyObjectMemoryAllocator()
        {
            _chunks = new PyObject?[4][];
            _previousFreePtrs = new FreePtr[16];

            _freePtr = new FreePtr { Chunk = -1, Index = DataChunkSize };
        }

        public Memory<PyObject?> Alloc(int size)
        {
            Debug.Assert(size > 0);
            Debug.Assert(size <= DataChunkSize);

            int targetChunk = _freePtr.Chunk;
            int index = _freePtr.Index;

            if (index + size > DataChunkSize)
            {
                targetChunk++;
                index = 0;

                if (targetChunk == _chunkCount)
                    ArrayStackHelper.Push(ref _chunks, ref _chunkCount, ArrayPool<PyObject?>.Shared.Rent(DataChunkSize));
            }

            ArrayStackHelper.Push(ref _previousFreePtrs, ref _previousFreePtrsCount, _freePtr);

            _freePtr = new FreePtr
            {
                Chunk = targetChunk,
                Index = index + size,
            };

            return new Memory<PyObject?>(_chunks[targetChunk], index, size);
        }

        public void Free(Memory<PyObject?> memory)
        {
            memory.Span.Clear();
            _freePtr = ArrayStackHelper.Pop(_previousFreePtrs, ref _previousFreePtrsCount);
        }

        public void Dispose()
        {
            for (int i = 0; i < _chunkCount; i++)
            {
                ArrayPool<PyObject?>.Shared.Return(_chunks[i], clearArray: true);
                _chunks[i] = null!;
            }
            _chunkCount = 0;
            _previousFreePtrsCount = 0;
        }
    }
}
using PySharp.Modules.Builtins;
using PySharp.Runtime.VirtualMachine;
using PySharp.Utility;
using System.Buffers;
using System.Diagnostics;

namespace PySharp.Runtime.Calls;

internal sealed partial class PyCallContextFrameState : IDisposable
{
    internal const int MaxRecursionDepth = 1000;
    internal const int MaxFramesStatesDiff = 100;
    private const int FrameBlockSize = 64;

    private PyInternalFrame[][] _blocks;
    private int _blockCount;
    private int _currentBlockIndex;
    private int _currentSlotIndex;
    private int _frameCount;
    private BytecodeVirtualMachineStates[] _statesStack;
    private int _statesCount;
    private readonly PyObjectMemoryAllocator _allocator;

    internal PyCallContextFrameState(PyInternalFrame rootFrame)
    {
        _blocks = new PyInternalFrame[4][];
        _blocks[0] = ArrayPool<PyInternalFrame>.Shared.Rent(FrameBlockSize);
        _blocks[0][0] = rootFrame;
        _blockCount = 1;
        _currentBlockIndex = 0;
        _currentSlotIndex = 0;
        _frameCount = 1;
        _statesStack = new BytecodeVirtualMachineStates[4];
        _statesCount = 0;
        _allocator = new PyObjectMemoryAllocator();
    }

    internal int CurrentFrameCount => _frameCount;
    internal ref PyInternalFrame CurrentInternalFrame => ref _blocks[_currentBlockIndex][_currentSlotIndex];

    internal ref PyInternalFrame GetFrame(int index)
    {
        Debug.Assert(index >= 0 && index < _frameCount);
        return ref _blocks[index / FrameBlockSize][index % FrameBlockSize];
    }

    public void EnterFrame(ref PyInternalFrame frame)
    {
        if (_frameCount is MaxRecursionDepth || (_frameCount - _statesCount > MaxFramesStatesDiff))
            throw new PyRuntimeException(PyRecursionErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Runtime_Recursion_MaxRecursionDepthExceeded)));

        _currentSlotIndex++;
        if (_currentSlotIndex >= FrameBlockSize)
        {
            _currentBlockIndex++;
            if (_currentBlockIndex >= _blocks.Length)
                Array.Resize(ref _blocks, _blocks.Length * 2);
            _blocks[_currentBlockIndex] ??= ArrayPool<PyInternalFrame>.Shared.Rent(FrameBlockSize);
            _currentSlotIndex = 0;
        }

        _blocks[_currentBlockIndex][_currentSlotIndex] = frame;
        _frameCount++;
        _blockCount = Math.Max(_blockCount, _currentBlockIndex + 1);
        frame = ref CurrentInternalFrame;
    }

    public void ExitInternalFrame(PyCallContext context, bool dispose)
    {
        if (_frameCount is 0)
            throw new InvalidOperationException("Could not exit frame, because it is the root");

        ref var frame = ref CurrentInternalFrame;
        if (dispose)
            frame.Dispose(context);
        _blocks[_currentBlockIndex][_currentSlotIndex] = default;
        _frameCount--;
        _currentSlotIndex--;

        if (_currentSlotIndex < 0 && _frameCount > 0)
        {
            _currentBlockIndex--;
            _currentSlotIndex = FrameBlockSize - 1;
        }
    }

    public void PushStates(ref BytecodeVirtualMachineStates states)
    {
        ArrayStackHelper.Push(ref _statesStack, ref _statesCount, states);
    }

    public BytecodeVirtualMachineStates PopStates()
    {
        return ArrayStackHelper.Pop(_statesStack, ref _statesCount);
    }

    public ref PyInternalFrame FindOuterNonInlineFrame()
    {
        var remaining = _frameCount;
        var blockIdx = _currentBlockIndex;
        var slotIdx = _currentSlotIndex;

        while (remaining > 0)
        {
            for (int i = slotIdx; i >= 0; i--)
            {
                ref var frame = ref _blocks[blockIdx][i];
                if (frame.FrameType is not FrameType.Comprehension)
                    return ref frame;
                remaining--;
            }
            blockIdx--;
            slotIdx = FrameBlockSize - 1;
        }
        throw new UnreachableException();
    }

    public Memory<PyObject?> Alloc(int size)
    {
        return _allocator.Alloc(size);
    }

    public void Free(Memory<PyObject?> memory)
    {
        _allocator.Free(memory);
    }

    public void Dispose()
    {
        if (_frameCount is -1)
            return;

        for (int i = 0; i < _blockCount; i++)
        {
            if (_blocks[i] is not null)
                ArrayPool<PyInternalFrame>.Shared.Return(_blocks[i], clearArray: true);
        }
        _allocator.Dispose();
        _frameCount = -1;
        _blocks = null!;
        _statesStack = null!;
    }
}

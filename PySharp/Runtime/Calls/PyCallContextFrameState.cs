using PySharp.Modules.Builtins;
using PySharp.Runtime.VirtualMachine;
using PySharp.Utility;
using System.Diagnostics;
using System.Xml.Linq;

namespace PySharp.Runtime.Calls;

internal sealed partial class PyCallContextFrameState : IDisposable
{
    internal const int MaxRecursionDepth = 1000;
    internal const int MaxFramesStatesDiff = 100;

    private PyInternalFrame[] _frames;
    private BytecodeVirtualMachineStates[] _statesStack;
    private int _frameCount;
    private int _statesCount;
    private readonly PyObjectMemoryAllocator _allocator;

    internal PyCallContextFrameState(PyInternalFrame rootFrame)
    {
        _frames = new PyInternalFrame[4];
        _frames[0] = rootFrame;
        _frameCount = 1;
        _statesStack = new BytecodeVirtualMachineStates[4];
        _statesCount = 0;
        _allocator = new PyObjectMemoryAllocator();
    }

    internal int CurrentFrameCount => _frameCount;
    internal int CurrentFrameIndex => _frameCount - 1;
    internal ref PyInternalFrame CurrentInternalFrame => ref _frames[CurrentFrameIndex];
    internal Span<PyInternalFrame> Frames => _frames.AsSpan()[.._frameCount];

    public void EnterFrame(ref PyInternalFrame frame)
    {
        if (_frameCount == MaxRecursionDepth || (_frameCount - _statesCount > MaxFramesStatesDiff))
            // if the diff is large, that means few calls are inlined (vm states are stored at stack)
            // that may lead to stack overflow on .NET
            throw new PyRuntimeException(PyRecursionErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Runtime_Recursion_MaxRecursionDepthExceeded)));

        ArrayStackHelper.Push(ref _frames, ref _frameCount, frame);
        frame = ref CurrentInternalFrame;
    }

    public void ExitInternalFrame(PyCallContext context, bool dispose)
    {
        if (_frameCount is 0)
            throw new InvalidOperationException("Could not exit frame, because it is the root");

        ref var frame = ref CurrentInternalFrame;
        if (dispose)
            frame.Dispose(context);
        _frames[--_frameCount] = default;
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
        for (int i = _frameCount - 1; i >= 0; --i)
        {
            ref var frame = ref _frames[i];
            if (frame.FrameType is not FrameType.Comprehension)
                return ref frame;
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

        _frames.AsSpan().Clear();
        _allocator.Dispose();
        _frameCount = -1;
    }
}

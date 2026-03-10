using PySharp.Modules.Builtins;
using System.Diagnostics;

namespace PySharp.Runtime.Calls;

internal sealed partial class PyCallContextFrameState : IDisposable
{
    private PyInternalFrame[] _frames;
    private int _frameCount;
    private readonly PyObjectMemoryAllocator _allocator;

    internal PyCallContextFrameState(PyInternalFrame rootFrame)
    {
        _frames = new PyInternalFrame[4];
        _frames[0] = rootFrame;
        _frameCount = 1;
        _allocator = new PyObjectMemoryAllocator();
    }

    internal int CurrentFrameCount => _frameCount;
    internal int CurrentFrameIndex => _frameCount - 1;
    internal ref PyInternalFrame CurrentInternalFrame => ref _frames[CurrentFrameIndex];
    internal Span<PyInternalFrame> Frames => _frames.AsSpan()[.._frameCount];

    public void EnterFrame(ref PyInternalFrame frame)
    {
        Debug.Assert(frame.BackFrameIndex == CurrentFrameIndex);

        if (_frameCount == _frames.Length)
            Array.Resize(ref _frames, _frames.Length * 2);

        _frames[_frameCount++] = frame;
        frame = ref CurrentInternalFrame;
    }

    public void ExitInternalFrame(PyCallContext context, bool dispose)
    {
        ref var frame = ref CurrentInternalFrame;
        if (frame.BackFrameIndex is -1)
            throw new InvalidOperationException("Could not exit frame, because it is the root frame");

        Debug.Assert(frame.BackFrameIndex == CurrentFrameIndex - 1);

        if (dispose)
            frame.Dispose(context);
        _frames[--_frameCount] = default;
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

using System.Diagnostics;

namespace PySharp.PyRuntime.Calls;

internal sealed class PyCallContextFrameState
{
    private readonly Lock _lock;
    private int _asyncModeCounter;
    private PyFrame _currentSyncFrame;
    private AsyncLocal<PyFrame>? _currentAsyncFrame;

    internal PyCallContextFrameState(PyFrame rootFrame)
    {
        _lock = new Lock();
        _asyncModeCounter = 0;
        _currentSyncFrame = rootFrame;
    }

    public PyFrame CurrentFrame
    {
        get
        {
            lock (_lock)
            {
                if (_currentAsyncFrame is null)
                    return _currentSyncFrame;

                var frame = _currentAsyncFrame.Value;
                Debug.Assert(frame is not null);
                return frame;
            }
        }
        set
        {
            lock (_lock)
            {
                if (_currentAsyncFrame is not null)
                    throw new NotSupportedException("Cannot directly set CurrentFrame while in async mode");

                _currentSyncFrame = value;
            }
        }
    }

    public void EnterFrame(PyFrame frame)
    {
        lock (_lock)
        {
            if (_currentAsyncFrame is null)
            {
                _currentSyncFrame = frame;
                return;
            }

            _currentAsyncFrame.Value = frame;
        }
    }

    public void ExitFrame()
    {
        lock (_lock)
        {
            if (_currentAsyncFrame is null)
            {
                if (_currentSyncFrame.Back is null)
                    throw new InvalidOperationException("Could not exit frame, because it is the root frame");

                _currentSyncFrame = _currentSyncFrame.Back;
                return;
            }

            var frame = _currentAsyncFrame.Value;
            Debug.Assert(frame is not null);

            if (ReferenceEquals(frame, _currentSyncFrame))
                throw new InvalidOperationException("Cannot exit frame: attempted to exit the root frame of async mode without exiting async mode first.");

            Debug.Assert(frame.Back is not null);
            _currentAsyncFrame.Value = frame.Back;
        }
    }

    public void EnterAsyncMode()
    {
        Debug.Assert(_asyncModeCounter >= 0);

        lock (_lock)
        {
            _asyncModeCounter++;

            if (_currentAsyncFrame is not null)
                return;

            _currentAsyncFrame = new AsyncLocal<PyFrame> { Value = _currentSyncFrame };
        }
    }

    public void ExitAsyncMode()
    {
        lock (_lock)
        {
            if (_asyncModeCounter <= 0)
                throw new InvalidOperationException("Cannot exit async mode when not in async mode.");

            _asyncModeCounter--;

            if (_asyncModeCounter > 0)
                return;

            Debug.Assert(_currentAsyncFrame is not null);
            if (!ReferenceEquals(_currentAsyncFrame.Value, _currentSyncFrame))
                throw new InvalidOperationException("Cannot exit async mode: current async frame does not match the root sync frame.");
            
            _currentAsyncFrame = null;
        }
    }
}

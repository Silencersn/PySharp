using PySharp.PyRuntime.Environments;

namespace PySharp.PyRuntime.Calls;

public sealed partial class PyCallContext
{
    internal static PyCallContext NotImplemented { get; } = new("[Not Implemented]");
    internal static PyCallContext NonContextDependency { get; } = new("[Non Context Dependency]");
    internal static PyCallContext CSharpRuntime { get; } = new("[CSharp Runtime]");
    internal static PyCallContext PyObjectComparison { get; } = new("[PyObject Comparison]");

    private readonly string _prompt;
    private readonly PyInterpreter _interpreter;
    private PyCallContextFrameState? _state;

    internal PyEnvironment PyEnvironment => _interpreter.PyEnvironment;
    internal PyInterpreter Interpreter => _interpreter;
    internal PyCallContextFrameState FrameState => _state ?? throw new InvalidOperationException("Context has not been initialized.");

    private PyCallContext(string prompt)
    {
        _prompt = prompt;
        _interpreter = null!;
        _state = null!;
    }
    private PyCallContext(string prompt, PyInterpreter interpreter)
    {
        _prompt = prompt;
        _interpreter = interpreter;
    }

    internal TextReader In => PyEnvironment.In;
    internal TextWriter Out => PyEnvironment.Out;
    internal TextWriter Error => PyEnvironment.Error;
    internal PyFrame CurrentFrame => FrameState.CurrentFrame;
    internal bool IsInteractive => PyEnvironment.IsInteractive;

    private void InitState(PyFrame rootFrame)
    {
        _state = new PyCallContextFrameState(rootFrame);
    }

    public readonly ref struct FrameSetter : IDisposable
    {
        private readonly PyCallContext _context;
        private readonly Action? _onExited;

        public FrameSetter(PyCallContext context, PyFrame frame, Action? onExited)
        {
            _context = context;
            _onExited = onExited;
            _context.FrameState.EnterFrame(frame);
        }

        void IDisposable.Dispose()
        {
            if (_context is null)
                // default(FrameSetter)
                return;

            _context.FrameState.ExitFrame();
            _onExited?.Invoke();
        }
    }

    internal FrameSetter WithFrame(PyFrame frame, Action? onExited = null)
    {
        return new FrameSetter(this, frame, onExited);
    }

    internal void EnsureFrameState(PyFrame expectedFrame)
    {
        if (ReferenceEquals(FrameState.CurrentFrame, expectedFrame))
            return;

        var currentFrame = FrameState.CurrentFrame;
        while (currentFrame is not null && !ReferenceEquals(currentFrame, expectedFrame))
            currentFrame = currentFrame.Back;

        if (currentFrame is not null)
        {
            FrameState.CurrentFrame = currentFrame;
            return;
        }

        throw new InvalidOperationException("Failed to restore the frame state.");
    }

    public readonly ref struct AsyncModeSetter : IDisposable
    {
        private readonly PyCallContext _context;

        public AsyncModeSetter(PyCallContext context)
        {
            _context = context;
            _context.FrameState.EnterAsyncMode();
        }

        void IDisposable.Dispose()
        {
            if (_context is null)
                // default(AsyncModeSetter)
                return;

            _context.FrameState.ExitAsyncMode();
        }
    }

    public AsyncModeSetter WithAsyncMode()
    {
        return new AsyncModeSetter(this);
    }

    internal void Exit(int exitCode)
    {
        PyEnvironment.ExitCode = exitCode;
        throw ThrowableSystemExit();
    }

    internal static PyCallContext CreateInterpreterMainContext(PyInterpreter interpreter)
    {
        var context = new PyCallContext("[Interpreter Main Context]", interpreter);
        var frame = PyFrame.CreateModuleFrame(context, null);
        context.InitState(frame);
        return context;
    }

    internal static PyCallContext FromCreatingThread(PyCallContext context)
    {
        var frame = context.CurrentFrame.CreateThreadRootFrame();
        var threadContext = new PyCallContext("[From Creating Thread]", context._interpreter);
        threadContext.InitState(frame);
        return threadContext;
    }

    public override string ToString()
    {
        return _prompt;
    }
}

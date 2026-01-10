using PySharp.PyRuntime.Environments;
using System.Diagnostics;

namespace PySharp.PyRuntime.Calls;

public sealed partial class PyCallContext
{
    internal static PyCallContext NotImplemented { get; } = new("[Not Implemented]");
    internal static PyCallContext NonContextDependency { get; } = new("[Non Context Dependency]");
    internal static PyCallContext CSharpRuntime { get; } = new("[CSharp Runtime]");

    private readonly string _prompt;
    private readonly PyEnvironment _environment;
    private PyCallContextState? _state;

    internal PyEnvironment PyEnvironment => _environment;
    internal PyCallContextState State => _state ?? throw new InvalidOperationException("Context has not been initialized.");

    private PyCallContext(string prompt)
    {
        _prompt = prompt;
        _environment = null!;
        _state = null!;
    }
    private PyCallContext(string prompt, PyEnvironment environment)
    {
        _prompt = prompt;
        _environment = environment;
    }

    internal TextReader In => PyEnvironment.In;
    internal TextWriter Out => PyEnvironment.Out;
    internal TextWriter Error => PyEnvironment.Error;
    internal PyFrame CurrentFrame => State.CurrentFrame;
    internal bool IsInteractive => PyEnvironment.IsInteractive;

    private void InitState(PyFrame rootFrame)
    {
        _state = new PyCallContextState(rootFrame);
    }

    public readonly ref struct FrameSetter : IDisposable
    {
        private readonly PyCallContext _context;
        private readonly Action? _onExited;

        public FrameSetter(PyCallContext context, PyFrame frame, Action? onExited)
        {
            _context = context;
            _onExited = onExited;
            _context.EnterFrame(frame);
        }

        void IDisposable.Dispose()
        {
            if (_context is null)
                // default(FrameSetter)
                return;

            _context.ExitFrame();
            _onExited?.Invoke();
        }
    }

    internal FrameSetter WithFrame(PyFrame frame, Action? onExited = null)
    {
        return new FrameSetter(this, frame, onExited);
    }

    internal void EnterFrame(PyFrame frame)
    {
        State.CurrentFrame = frame;
    }

    internal void ExitFrame()
    {
        Debug.Assert(State.CurrentFrame.Back is not null);
        State.CurrentFrame = State.CurrentFrame.Back;
    }

    internal void EnsureFrameState(PyFrame expectedFrame)
    {
        if (ReferenceEquals(State.CurrentFrame, expectedFrame))
            return;

        var currentFrame = State.CurrentFrame;
        while (currentFrame is not null && !ReferenceEquals(currentFrame, expectedFrame))
            currentFrame = currentFrame.Back;

        if (currentFrame is not null)
        {
            State.CurrentFrame = currentFrame;
            return;
        }

        throw new InvalidOperationException("Failed to restore the frame state.");
    }

    internal void Exit(int exitCode)
    {
        PyEnvironment.ExitCode = exitCode;
        throw ThrowableSystemExit();
    }

    public static PyCallContext FromLoadingModule(PyEnvironment environment)
    {
        var context = new PyCallContext("[From Loading Module]", environment);
        var frame = PyFrame.CreateModuleFrame(context, null);
        context.InitState(frame);
        return context;
    }

    internal static PyCallContext FromCreatingThread(PyCallContext context)
    {
        var frame = context.CurrentFrame.CreateThreadRootFrame();
        var threadContext = new PyCallContext("[From Creating Thread]", context.PyEnvironment);
        threadContext.InitState(frame);
        return threadContext;
    }

    public override string ToString()
    {
        return _prompt;
    }
}

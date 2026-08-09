using PySharp.Runtime.Comparison;
using PySharp.Runtime.Environments;
using PySharp.Utility;

namespace PySharp.Runtime.Calls;

public sealed partial class PyCallContext : IDisposable
{
    internal static PyCallContext NotImplemented { get; } = new("[Not Implemented]");
    internal static PyCallContext NonContextDependency { get; } = new("[Non Context Dependency]");
    internal static PyCallContext CSharpRuntime { get; } = new("[CSharp Runtime]");
    internal static PyCallContext PyObjectComparison { get; } = new("[PyObject Comparison]");

    private readonly string _prompt;
    private readonly PyEnvironment _environment;
    private readonly PyInterpreter? _interpreter;
    private PyCallContextFrameState? _state;
    private ImmutableArrayBuilderPool? _builderPool;

    internal PyEnvironment PyEnvironment => _environment;
    internal PyInterpreter? Interpreter => _interpreter;
    internal PyCallContextFrameState FrameState => _state ?? throw new InvalidOperationException("Context is not initialized or is disposed.");
    public PyObjectComparer Comparer => field ??= new PyObjectComparer(this);
    internal ImmutableArrayBuilderPool BuilderPool => _builderPool ??= new();

    private PyCallContext(string prompt) : this(prompt, PyEnvironment.CreateNull())
    {
    }
    private PyCallContext(string prompt, PyInterpreter interpreter) : this(prompt, interpreter.PyEnvironment)
    {
        _interpreter = interpreter;
    }
    private PyCallContext(string prompt, PyEnvironment environment)
    {
        _prompt = prompt;
        _environment = environment;
    }

    internal TextReader In => PyEnvironment.In;
    internal TextWriter Out => PyEnvironment.Out;
    internal TextWriter Error => PyEnvironment.Error;
    internal ref PyInternalFrame CurrentInternalFrame => ref FrameState.CurrentInternalFrame;
    internal bool IsInteractive => PyEnvironment.IsInteractive;

    private void InitState(ref PyInternalFrame rootFrame)
    {
        _state = new PyCallContextFrameState(rootFrame);
    }

    internal readonly ref struct FrameSetter : IDisposable
    {
        private readonly PyCallContext _context;
        private readonly bool _dispose;

        internal FrameSetter(PyCallContext context, ref PyInternalFrame frame, bool dispose)
        {
            _context = context;
            _dispose = dispose;
            _context.FrameState.EnterFrame(ref frame);
        }

        void IDisposable.Dispose()
        {
            if (_context is null)
                // default(FrameSetter)
                return;

            _context.FrameState.ExitInternalFrame(_context, _dispose);
        }
    }

    internal FrameSetter WithFrame(ref PyInternalFrame frame, bool dispose = true)
    {
        return new FrameSetter(this, ref frame, dispose);
    }

    internal void Exit(int exitCode)
    {
        PyEnvironment.ExitCode = exitCode;
        throw SystemExit(string.Empty);
    }

    internal static PyCallContext CreateFromEnvironment(PyEnvironment? environment = null)
    {
        environment ??= PyEnvironment.CreateNull();
        return new PyCallContext("[From Environment]", environment)
        {
            _state = new PyCallContextFrameState(default /* TODO */)
        };
    }

    internal static PyCallContext CreateInterpreterMainContext(PyInterpreter interpreter)
    {
        var context = new PyCallContext("[Interpreter Main Context]", interpreter);
        var frame = PyInternalFrame.CreateModuleFrame(context, isRoot: true, PySpecialNames.Main);
        context.InitState(ref frame);
        return context;
    }

    internal static PyCallContext FromCreatingThread(PyCallContext context)
    {
        if (context._interpreter is null)
            throw new NotSupportedException("TODO");

        var frame = context.CurrentInternalFrame.CreateThreadRootFrame();
        var threadContext = new PyCallContext("[From Creating Thread]", context._interpreter);
        threadContext.InitState(ref frame);
        return threadContext;
    }

    public override string ToString()
    {
        return _prompt;
    }

    public void Dispose()
    {
        if (_state is null)
            return;

        _state.Dispose();
        _state = null;

        _builderPool?.Dispose();
        _builderPool = null;
    }
}

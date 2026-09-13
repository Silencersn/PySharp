using PySharp.Modules.Builtins;
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
    private PyCallContextFrameState? _state;
    private ImmutableArrayBuilderPool? _builderPool;

    // Dynamic-scope handled exception (CPython tstate->exc_info->exc_value,
    // read by bare raise): set while an exception is being handled by this
    // call chain — inside an except body, while passing through a finally, or
    // while a with statement dispatches __exit__. Frames save the previous
    // value when a handler is entered and restore it on exit, so callees
    // observe the caller's active exception without owning it.
    internal PyExceptionObject? HandledException { get; set; }

    internal PyEnvironment PyEnvironment => _environment;
    internal PyCallContextFrameState FrameState => _state ?? throw new InvalidOperationException("Context is not initialized or is disposed.");
    public PyObjectComparer Comparer => field ??= new PyObjectComparer(this);
    internal ImmutableArrayBuilderPool BuilderPool => _builderPool ??= new();

    private PyCallContext(string prompt) : this(prompt, PyEnvironment.CreateNull())
    {
    }
    private PyCallContext(string prompt, PyEnvironment environment)
    {
        _prompt = prompt;
        _environment = environment;
    }

    internal StreamReader In => PyEnvironment.In;
    internal StreamWriter Out => PyEnvironment.Out;
    internal StreamWriter Error => PyEnvironment.Error;
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
        throw ThrowableException(PySystemExitObjectType.Shared, PyIntObject.FromInteger(exitCode));
    }

    internal static PyCallContext CreateFromEnvironment(PyEnvironment? environment = null)
    {
        environment ??= PyEnvironment.CreateNull();
        return new PyCallContext("[From Environment]", environment)
        {
            _state = new PyCallContextFrameState(default /* TODO */)
        };
    }

    internal static PyCallContext CreateInterpreterRootContext(PyEnvironment environment)
    {
        var context = new PyCallContext("[Interpreter Root Context]", environment);
        var frame = PyInternalFrame.CreateModuleFrame(context, isRoot: true, PySpecialNames.Main);
        context.InitState(ref frame);
        return context;
    }

    internal static PyCallContext FromCreatingThread(PyCallContext context)
    {
        var frame = context.CurrentInternalFrame.CreateThreadRootFrame();
        var threadContext = new PyCallContext("[From Creating Thread]", context._environment);
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

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

    // Ambient context for the fixed-signature .NET faces (object.ToString,
    // Exception.Message, IComparer members) that cannot carry a context
    // parameter: the AsyncLocal analog of CPython's per-thread state read by
    // slot callbacks without a tstate argument. Published by the interpreter
    // root and thread factories only, restored on Dispose — compile paths and
    // the contract-value-typed collection faces never publish one and keep
    // their sentinels as the no-ambient fallback.
    private static readonly AsyncLocal<PyCallContext?> Ambient = new();

    private PyCallContext? _ambientPrevious;
    private bool _ambientPublished;
    private int? _ambientThreadId;

    // ExecutionContext flows the ambient into thread-pool continuations, but
    // a context is a single-thread mutable object (frame stack, handled
    // exception, builder pool); off-thread reads see null so callers fall
    // back to their sentinel instead of sharing the publishing thread's state.
    internal static PyCallContext? Current
    {
        get
        {
            var context = Ambient.Value;
            return context is not null && context._ambientThreadId == Environment.CurrentManagedThreadId
                ? context
                : null;
        }
    }

    private void PublishAmbient(bool inheritPrevious)
    {
        _ambientPrevious = inheritPrevious ? Ambient.Value : null;
        _ambientThreadId = Environment.CurrentManagedThreadId;
        _ambientPublished = true;
        Ambient.Value = this;
    }

    private void UnpublishAmbient()
    {
        if (!_ambientPublished)
            return;

        _ambientPublished = false;
        _ambientThreadId = null;
        if (Ambient.Value == this)
            Ambient.Value = _ambientPrevious;
    }

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
        context.PublishAmbient(inheritPrevious: true);
        return context;
    }

    internal static PyCallContext FromCreatingThread(PyCallContext context)
    {
        var frame = context.CurrentInternalFrame.CreateThreadRootFrame();
        var threadContext = new PyCallContext("[From Creating Thread]", context._environment);
        threadContext.InitState(ref frame);
        // Thread boundary: the parent ambient flowed in through ExecutionContext
        // is overwritten and deliberately not restored, so the parent context can
        // never leak back onto this thread once the thread context is disposed.
        threadContext.PublishAmbient(inheritPrevious: false);
        return threadContext;
    }

    public override string ToString()
    {
        return _prompt;
    }

    public void Dispose()
    {
        UnpublishAmbient();

        if (_state is null)
            return;

        _state.Dispose();
        _state = null;

        _builderPool?.Dispose();
        _builderPool = null;
    }
}

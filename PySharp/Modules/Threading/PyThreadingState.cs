namespace PySharp.Modules.Threading;

/// <summary>
/// Per-environment face of threading.py's module-level state: the _active
/// registry, the _main_thread object, and the configured thread stack
/// size. CPython keeps these on the threading module itself, which is a
/// per-interpreter singleton — one PyEnvironment here plays that role, so
/// test hosts running several environments in parallel do not observe each
/// other's threads.
/// </summary>
internal sealed class PyThreadingState
{
    // threading.py's _active registry: started threads keyed by ident,
    // unregistering as the worker exits. A plain Dictionary under a gate
    // (not a concurrent one) because enumerate() must report the insertion
    // order, which is the main thread first and then workers in start
    // order.
    internal readonly Dictionary<long, PyThreadObject> Active = [];

    internal readonly Lock Gate = new();

    // threading.py sets the interpreter's startup thread as _main_thread at
    // import time, keyed by the true main-thread ident; PySharp settles it
    // on the first import of the threading module, which in the standard
    // execution model is the same thread
    internal PyThreadObject? MainThread;

    // the configured stack size for threads created from now on, in bytes;
    // 0 means the platform default (threading.py's _thread.stack_size state)
    internal long StackSize;
}

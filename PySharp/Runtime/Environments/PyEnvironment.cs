using PySharp.Modules.Builtins;
using PySharp.Runtime.IO;
using PySharp.Utility;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.Runtime.Environments;

public sealed partial class PyEnvironment : IDisposable
{
    private readonly Stream _inStream;
    private readonly Stream _outStream;
    private readonly Stream _errorStream;
    private readonly StreamReader _in;
    private readonly StreamWriter _out;
    private readonly StreamWriter _error;
    private readonly bool _isInteractive;
    private readonly bool _supportsColorOut;
    private readonly bool _supportsColorError;
    private readonly List<string> _paths;
    private readonly List<string> _args;
    private readonly ConcurrentDictionary<string, object?> _envData;
    private bool _disposed;

    static PyEnvironment()
    {
    }

    internal PyEnvironment(
        PyEnvironmentHost host,
        bool isInteractive = false,
        IEnumerable<string>? paths = null,
        IEnumerable<string>? args = null,
        Encoding? stdinEncoding = null,
        Encoding? stdoutEncoding = null,
        Encoding? stderrEncoding = null,
        PyEnvironmentOptions? options = null,
        bool? supportsColorOut = null,
        bool? supportsColorError = null,
        IEnumerable<PyModuleProvider>? moduleProviders = null)
    {
        Host = host;
        _inStream = host.AllocateStdIn();
        _outStream = host.AllocateStdOut();
        _errorStream = host.AllocateStdErr();
        _in = new StreamReader(_inStream, stdinEncoding ?? Host.DefaultEncoding);
        _out = new StreamWriter(_outStream, stdoutEncoding ?? Host.DefaultEncoding);
        _error = new StreamWriter(_errorStream, stderrEncoding ?? Host.DefaultEncoding);
        _out.AutoFlush = true;
        _error.AutoFlush = true;
        _isInteractive = isInteractive;
        _supportsColorOut = supportsColorOut ?? Host.SupportsColorOutput;
        _supportsColorError = supportsColorError ?? Host.SupportsErrorColorOutput;
        _paths = paths is null ? [] : [.. paths];
        _args = args is null ? [] : [.. args];
        Options = options ?? PyEnvironmentOptions.Default;
        // Null keeps the default [Builtin, Path] chain; an explicit chain is
        // passed complete by the builder, where order is the resolution order.
        ModuleProviders = moduleProviders is null
            ? [BuiltinModuleProvider.Shared, PathProvider.Shared]
            : [.. moduleProviders];

        _envData = [];
    }

    public PyEnvironmentHost Host { get; }

    public bool OutSupportsColor => _supportsColorOut;
    public bool ErrorSupportsColor => _supportsColorError;

    internal StreamReader In => _in;
    internal StreamWriter Out => _out;
    internal StreamWriter Error => _error;
    internal Stream InStream => _inStream;
    internal Stream OutStream => _outStream;
    internal Stream ErrorStream => _errorStream;
    internal PyEnvironmentOptions Options { get; }
    internal Dictionary<string, PyModuleObject?> Modules { get; } = [];
    internal ConcurrentSet<Thread> Threads { get; } = [];
    internal List<string> Paths => _paths;
    internal List<string> Args => _args;
    internal List<PyModuleProvider> ModuleProviders { get; }
    internal int ExitCode { get; set; }
    internal bool IsInteractive => _isInteractive;
    internal IVirtualFileSystem FileSystem => Host.FileSystem;

    public PyStrObject.InternPool InternPool { get; } = new();

    /// <summary>
    /// Raised once when this environment is being disposed, before its
    /// registered threads are interrupted and the standard streams are
    /// released. Subscriber exceptions cannot stop the teardown, but they
    /// propagate to the <see cref="Dispose"/> caller once the teardown has
    /// completed. Hosts use this hook to reclaim values injected via
    /// <see cref="SetEnvData"/>, which the environment never disposes itself.
    /// </summary>
    public event Action? OnDisposing;

    /// <summary>
    /// Stores a host-supplied value under <paramref name="key"/> for the
    /// lifetime of this environment; setting an existing key replaces its
    /// value. Extension implementations read values back through the call
    /// context's <c>PyEnvironment</c>. The store is safe for concurrent
    /// reads and writes. Stored values are never wrapped as Python objects
    /// and cannot be reached from Python code. This environment never
    /// disposes stored values — the host owns them; subscribe to
    /// <see cref="OnDisposing"/> to reclaim them at teardown.
    /// </summary>
    public void SetEnvData(string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _envData[key] = value;
    }

    /// <summary>
    /// Removes the value stored under <paramref name="key"/>. Returns false
    /// when the key is not currently set on this environment.
    /// </summary>
    public bool RemoveEnvData(string key, out object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _envData.TryRemove(key, out value);
    }

    /// <summary>
    /// Reads the value stored under <paramref name="key"/>. Returns false
    /// when the key is not currently set on this environment.
    /// </summary>
    public bool TryGetEnvData(string key, out object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _envData.TryGetValue(key, out value);
    }

    /// <summary>
    /// Reads the value stored under <paramref name="key"/> as
    /// <typeparamref name="T"/>. Returns false when the key is not currently
    /// set on this environment or the stored value is not assignable to
    /// <typeparamref name="T"/>.
    /// </summary>
    public bool TryGetEnvData<T>(string key, [NotNullWhen(true)] out T? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (TryGetEnvData(key, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            OnDisposing?.Invoke();
        }
        finally
        {
            foreach (var thread in Threads)
                // this Interrupt calling may be failed
                //
                // if the thread could not be interrupted,
                // just wait to stay consistent with CPython
                //
                thread.Interrupt();
            foreach (var thread in Threads)
                thread.Join();

            _in.Dispose();
            _out.Dispose();
            _error.Dispose();
        }
    }

    public static PyEnvironment CreateNull()
    {
        return new PyEnvironment(PyEnvironmentHost.CreateNull(), isInteractive: true);
    }

    public static PyEnvironment CreateConsole()
    {
        return new PyEnvironment(PyEnvironmentHost.CreateConsole());
    }
}

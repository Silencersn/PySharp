using System.Text;

namespace PySharp.Runtime.Environments;

internal sealed class PyEnvironmentBuilder : IPyEnvironmentBuilder
{
    private readonly PyEnvironmentHost _host;
    private bool _isInteractive;
    private readonly List<string> _paths = [];
    private readonly List<string> _args = [];

    private Encoding? _stdinEncoding;
    private Encoding? _stdoutEncoding;
    private Encoding? _stderrEncoding;

    private bool? _supportsColorOut;
    private bool? _supportsColorError;

    private int _optimizationLevel;

    private bool _importSite;

    private readonly List<PyModuleProvider> _headModuleProviders = [];
    private readonly List<PyModuleProvider> _tailModuleProviders = [];
    private bool _moduleProvidersCleared;

    internal PyEnvironmentBuilder(PyEnvironmentHost host)
    {
        _importSite = true;
        _host = host;
    }

    public IPyEnvironmentBuilder SetInteractive(bool isInteractive)
    {
        _isInteractive = isInteractive;
        return this;
    }

    public IPyEnvironmentBuilder AddPath(string path)
    {
        _paths.Add(path);
        return this;
    }

    public IPyEnvironmentBuilder AddArg(string arg)
    {
        _args.Add(arg);
        return this;
    }

    public IPyEnvironmentBuilder UseStdInEncoding(Encoding encoding)
    {
        _stdinEncoding = encoding;
        return this;
    }

    public IPyEnvironmentBuilder UseStdOutEncoding(Encoding encoding)
    {
        _stdoutEncoding = encoding;
        return this;
    }

    public IPyEnvironmentBuilder UseStdErrEncoding(Encoding encoding)
    {
        _stderrEncoding = encoding;
        return this;
    }

    public IPyEnvironmentBuilder UseStdioEncoding(Encoding encoding)
    {
        _stdinEncoding = encoding;
        _stdoutEncoding = encoding;
        _stderrEncoding = encoding;
        return this;
    }

    public IPyEnvironmentBuilder UseStdOutColorSupport(bool enabled)
    {
        _supportsColorOut = enabled;
        return this;
    }

    public IPyEnvironmentBuilder UseStdErrColorSupport(bool enabled)
    {
        _supportsColorError = enabled;
        return this;
    }

    public IPyEnvironmentBuilder SetOptimizationLevel(int level)
    {
        _optimizationLevel = level;
        return this;
    }

    public IPyEnvironmentBuilder AddModuleProvider(PyModuleProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _tailModuleProviders.Add(provider);
        return this;
    }

    public IPyEnvironmentBuilder InsertModuleProvider(PyModuleProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _headModuleProviders.Insert(0, provider);
        return this;
    }

    public IPyEnvironmentBuilder ClearModuleProviders()
    {
        _headModuleProviders.Clear();
        _tailModuleProviders.Clear();
        _moduleProvidersCleared = true;
        return this;
    }

    public PyEnvironment Build()
    {
        var host = _host ?? PyEnvironmentHost.CreateNull();

        var options = new PyEnvironmentOptions()
        {
            NotImplyImportSite = !_importSite,
            OptimizationLevel = _optimizationLevel,
        };

        // An untouched builder keeps the default [Builtin, Path] chain; a
        // cleared or customized builder passes the complete chain, where
        // order is the resolution order. Inserts shadow everything (like
        // sys.meta_path.insert(0, finder)), adds are trailing fallbacks.
        List<PyModuleProvider>? moduleProviders = null;
        if (_moduleProvidersCleared)
            moduleProviders = [.. _headModuleProviders, .. _tailModuleProviders];
        else if (_headModuleProviders.Count > 0 || _tailModuleProviders.Count > 0)
            moduleProviders = [.. _headModuleProviders, PyModuleProvider.Builtin, PyModuleProvider.Path, .. _tailModuleProviders];

        var environment = new PyEnvironment(host, _isInteractive, _paths, _args,
            stdinEncoding: _stdinEncoding,
            stdoutEncoding: _stdoutEncoding,
            stderrEncoding: _stderrEncoding,
            options: options,
            supportsColorOut: _supportsColorOut,
            supportsColorError: _supportsColorError,
            moduleProviders: moduleProviders);

        return environment;
    }

    public IPyEnvironmentBuilder NotImplyImportSite()
    {
        _importSite = false;
        return this;
    }
}



public interface IPyEnvironmentBuilder
{
    IPyEnvironmentBuilder SetInteractive(bool isInteractive);
    IPyEnvironmentBuilder AddPath(string path);
    IPyEnvironmentBuilder AddArg(string arg);
    IPyEnvironmentBuilder UseStdInEncoding(Encoding encoding);
    IPyEnvironmentBuilder UseStdOutEncoding(Encoding encoding);
    IPyEnvironmentBuilder UseStdErrEncoding(Encoding encoding);
    IPyEnvironmentBuilder UseStdioEncoding(Encoding encoding);
    IPyEnvironmentBuilder UseStdOutColorSupport(bool enabled);
    IPyEnvironmentBuilder UseStdErrColorSupport(bool enabled);
    IPyEnvironmentBuilder SetOptimizationLevel(int level);
    IPyEnvironmentBuilder AddModuleProvider(PyModuleProvider provider);
    IPyEnvironmentBuilder InsertModuleProvider(PyModuleProvider provider);
    IPyEnvironmentBuilder ClearModuleProviders();
    IPyEnvironmentBuilder AddArgs(IEnumerable<string>? args)
    {
        if (args is null)
            return this;

        foreach (var arg in args)
            AddArg(arg);

        return this;
    }
    IPyEnvironmentBuilder NotImplyImportSite();
    PyEnvironment Build();
}

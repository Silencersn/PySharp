using System.Text;

namespace PySharp.Runtime.Environments;

internal sealed class PyEnvironmentBuilder :
    IPyEnvironmentBuilder,
    IPyEnvironmentInitializationBuilder
{
    private readonly PyEnvironmentHost _host;
    private bool _isInteractive;
    private readonly List<string> _paths = [];
    private readonly List<string> _args = [];

    private Encoding? _stdinEncoding;
    private Encoding? _stdoutEncoding;
    private Encoding? _stderrEncoding;

    private bool _syncExit;
    private bool _importSite;

    internal PyEnvironmentBuilder(PyEnvironmentHost host)
    {
        _syncExit = false;
        _importSite = true;
        _host = host;
    }

    public IPyEnvironmentInitializationBuilder Initialization => this;

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

    public PyEnvironment Build()
    {
        var host = _host ?? PyEnvironmentHost.CreateNull();

        var options = new PyEnvironmentOptions()
        {
            NotImplyImportSite = !_importSite,
        };

        var environment = new PyEnvironment(host, _isInteractive, _paths, _args,
            stdinEncoding: _stdinEncoding,
            stdoutEncoding: _stdoutEncoding,
            stderrEncoding: _stderrEncoding,
            options: options);

        if (_syncExit)
            environment.Exit += static args => Environment.Exit(args.ExitCode);

        return environment;
    }

    IPyEnvironmentInitializationBuilder IPyEnvironmentInitializationBuilder.SyncExit()
    {
        _syncExit = true;
        return this;
    }

    IPyEnvironmentInitializationBuilder IPyEnvironmentInitializationBuilder.NotImplyImportSite()
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
    IPyEnvironmentBuilder AddArgs(IEnumerable<string>? args)
    {
        if (args is null)
            return this;

        foreach (var arg in args)
            AddArg(arg);

        return this;
    }
    IPyEnvironmentInitializationBuilder Initialization { get; }
    PyEnvironment Build();
}

public interface IPyEnvironmentInitializationBuilder : IPyEnvironmentBuilder
{
    IPyEnvironmentInitializationBuilder SyncExit();
    IPyEnvironmentInitializationBuilder NotImplyImportSite();
}

namespace PySharp.Runtime.Environments;

internal sealed class PyEnvironmentBuilder :
    IPyEnvironmentBuilder,
    IPyEnvironmentInitializationBuilder
{
    private readonly PyEnvironmentHost _host;
    private bool _isInteractive;
    private readonly List<string> _paths = [];
    private readonly List<string> _args = [];

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

    public PyEnvironment Build()
    {
        var host = _host ?? PyEnvironmentHost.CreateNull();

        var environment = new PyEnvironment(host, _isInteractive, _paths, _args);

        var options = new PyEnvironmentOptions()
        {
            NotImplyImportSite = !_importSite,
        };

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
    IPyEnvironmentInitializationBuilder Initialization { get; }
    PyEnvironment Build();
}

public interface IPyEnvironmentInitializationBuilder : IPyEnvironmentBuilder
{
    IPyEnvironmentInitializationBuilder SyncExit();
    IPyEnvironmentInitializationBuilder NotImplyImportSite();
}

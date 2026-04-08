using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using PySharp.Runtime.IO.Physical;

namespace PySharp.Runtime.Environments;

internal sealed class PyEnvironmentBuilder :
    IPyEnvironmentBuilder,
    IPyEnvironmentInitializationBuilder
{
    private PyEnvironmentHost? _host;

    private bool _syncExit;
    private bool _importSite;

    internal PyEnvironmentBuilder()
    {
        _syncExit = false;
        _importSite = true;
    }

    public IPyEnvironmentInitializationBuilder Initialization => this;

    public IPyEnvironmentBuilder UseHost(PyEnvironmentHost host)
    {
        _host = host;
        return this;
    }

    public PyEnvironment Build()
    {
        var host = _host ?? PyEnvironmentHost.CreateNull();

        var environment = new PyEnvironment(host);

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
    IPyEnvironmentBuilder UseHost(PyEnvironmentHost host);
    IPyEnvironmentInitializationBuilder Initialization { get; }
    PyEnvironment Build();
}

public interface IPyEnvironmentInitializationBuilder : IPyEnvironmentBuilder
{
    IPyEnvironmentInitializationBuilder SyncExit();
    IPyEnvironmentInitializationBuilder NotImplyImportSite();
}

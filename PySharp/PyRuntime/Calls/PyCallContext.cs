using PySharp.PyRuntime.Environments;

namespace PySharp.PyRuntime.Calls;

public class PyCallContext
{
    internal static PyCallContext Null { get; } = new(null!);
    internal static PyCallContext NonContextDependency { get; } = new(null!);

    private readonly PyEnvironment _environment;

    internal PyEnvironment PyEnvironment => _environment;

    private PyCallContext(PyEnvironment environment)
    {
        _environment = environment;
    }

    internal static PyCallContext FromEnvironment(PyEnvironment environment)
    {
        return new PyCallContext(environment);
    }
}

using PySharp.PyRuntime.Environments;

namespace PySharp.PyRuntime.Calls;

public class PyCallContext
{
    internal static PyCallContext Null { get; } = new("[Null]", null!);
    internal static PyCallContext NonContextDependency { get; } = new("[Non Context Dependency]", null!);

    private readonly string _prompt;
    private readonly PyEnvironment _environment;

    internal PyEnvironment PyEnvironment => _environment;

    private PyCallContext(string prompt, PyEnvironment environment)
    {
        _prompt = prompt;
        _environment = environment;
    }

    internal TextReader In => PyEnvironment.In;
    internal TextWriter Out => PyEnvironment.Out;
    internal TextWriter Error => PyEnvironment.Error;
    internal PyFrame CurrentFrame => PyEnvironment.CurrentFrame;
    internal bool IsInteractive => PyEnvironment.IsInteractive;

    internal static PyCallContext FromEnvironment(PyEnvironment environment)
    {
        return new PyCallContext("[From Environment]", environment);
    }

    public override string ToString()
    {
        return _prompt;
    }
}

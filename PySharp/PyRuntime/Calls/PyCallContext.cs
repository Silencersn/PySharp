namespace PySharp.PyRuntime.Calls;

public class PyCallContext
{
    internal static PyCallContext Null { get; } = new();
    internal static PyCallContext NonContextDependency { get; } = new();
    internal PyCallContext()
    {
    }
}

namespace PySharp.PyRuntime.Calls;

public class PyCallContext
{
    internal static PyCallContext Null { get; } = new();
    internal PyCallContext()
    {
    }
}

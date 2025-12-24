using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Time;

public static class PyTimeFunctions
{
    public static readonly PyBuiltinFunctionOrMethodObject Time = new("time", TimeImpl);

    [PyFunctionArgsDef()]
    private static PyResult TimeImpl(PyCallContext context, PyArguments arguments)
    {
        var span = DateTime.UtcNow - DateTime.UnixEpoch;
        var seconds = span.TotalSeconds;
        return PyFloatObject.FromDouble(seconds);
    }
}

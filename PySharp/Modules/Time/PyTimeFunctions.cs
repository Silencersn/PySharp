using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Time;

public static class PyTimeFunctions
{
    public static readonly PyBuiltinFunctionOrMethodObject Time = PyBuiltinFunctionOrMethodObject.CreateFunction("time", TimeImpl);

    [PyFunctionParameters()]
    private static PyResult TimeImpl(PyCallContext context, PyArguments arguments)
    {
        var span = DateTime.UtcNow - DateTime.UnixEpoch;
        var seconds = span.TotalSeconds;
        return PyFloatObject.FromDouble(seconds);
    }
}

using PySharp.PyModules.Builtins;
using PySharp.PyRuntime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyModules.Time;

public static class PyTimeFunctions
{
    public static readonly PyBuiltinFunctionOrMethodObject Time = new("time", TimeImpl);

    [PyFunctionArgsDef()]
    private static PyFloatObject TimeImpl(PyArguments arguments)
    {
        var span = DateTime.UtcNow - DateTime.UnixEpoch;
        var seconds = span.TotalSeconds;
        return PyFloatObject.FromDouble(seconds);
    }
}

using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Frozen;

namespace PySharp.PyModules.Builtins;

partial class PyObject
{
    public PyObject? Iter()
    {
        return PyType.Iter(PyCallContext.Null, this).Value;
    }
    public PyObject? Next()
    {
        return PyType.Next(PyCallContext.Null, this).Value;
    }
    public PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyType.Call(PyCallContext.Null, this, args, kwargs).Value;
    }
}

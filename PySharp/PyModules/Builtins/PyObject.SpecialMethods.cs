using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Frozen;

namespace PySharp.PyModules.Builtins;

partial class PyObject
{
    public PyObject? Repr()
    {
        return PyType.Repr(PyCallContext.Null, this).Value;
    }
    public PyObject? Str()
    {
        return PyType.Str(PyCallContext.Null, this).Value;
    }
    public PyObject? Hash()
    {
        return PyType.Hash(PyCallContext.Null, this).Value;
    }
    public PyObject? Bool()
    {
        return PyType.Bool(PyCallContext.Null, this).Value;
    }
    public PyObject? Int()
    {
        return PyType.Int(PyCallContext.Null, this).Value;
    }
    public PyObject? Float()
    {
        return PyType.Float(PyCallContext.Null, this).Value;
    }
    public PyObject? Index()
    {
        return PyType.Index(PyCallContext.Null, this).Value;
    }
    public PyObject? Len()
    {
        return PyType.Len(PyCallContext.Null, this).Value;
    }
    public PyObject? Iter()
    {
        return PyType.Iter(PyCallContext.Null, this).Value;
    }
    public PyObject? Next()
    {
        return PyType.Next(PyCallContext.Null, this).Value;
    }
    public PyObject? DivMod(PyObject other)
    {
        return PyType.DivMod(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RDivMod(PyObject other)
    {
        return PyType.RDivMod(PyCallContext.Null, this, other).Value;
    }
    public PyObject? SetName(PyObject owner, PyObject name)
    {
        return PyType.SetName(PyCallContext.Null, this, owner, name).Value;
    }
    public PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyType.Call(PyCallContext.Null, this, args, kwargs).Value;
    }
    public PyObject? GetAttribute(string name)
    {
        return PyType.GetAttribute(PyCallContext.Null, this, name).Value;
    }
    public PyObject? Format(string formatSpec)
    {
        return PyType.Format(PyCallContext.Null, this, formatSpec).Value;
    }
}

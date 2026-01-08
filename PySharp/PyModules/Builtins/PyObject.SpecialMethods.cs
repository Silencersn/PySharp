using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

partial class PyObject
{
    // TODO: find __xxxx__ in mro?

    public PyResult Complex(PyCallContext context)
    {
        return PyType.Complex(context, this);
    }
    public PyResult Abs(PyCallContext context)
    {
        return PyType.Abs(context, this);
    }
    public PyResult Neg(PyCallContext context)
    {
        return PyType.Neg(context, this);
    }
    public PyResult Pos(PyCallContext context)
    {
        return PyType.Pos(context, this);
    }
    public PyResult Invert(PyCallContext context)
    {
        return PyType.Invert(context, this);
    }
    public PyResult DivMod(PyCallContext context, PyObject other)
    {
        return PyType.DivMod(context, this, other);
    }
    public PyResult RDivMod(PyCallContext context, PyObject other)
    {
        return PyType.RDivMod(context, this, other);
    }
    public PyResult SetName(PyCallContext context, PyObject owner, PyObject name)
    {
        return PyType.SetName(context, this, owner, name);
    }
    public PyResult Missing(PyCallContext context, PyObject key)
    {
        return PyType.Missing(context, this, key);
    }
    public PyResult Init(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyType.Init(context, this, args, kwargs);
    }
    public PyResult GetAttribute(PyCallContext context, string name)
    {
        return PyType.GetAttribute(context, this, PyStrObject.FromString(name));
    }
    public PyResult Format(PyCallContext context, string formatSpec)
    {
        return PyType.Format(context, this, formatSpec);
    }
}

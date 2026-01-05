using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

partial class PyObject
{        
    // TODO: find __xxxx__ in mro?

    public PyResult Repr(PyCallContext context)
    {
        return PyType.Repr(context, this);
    }
    public PyResult Str(PyCallContext context)
    {
        return PyType.Str(context, this);
    }
    public PyResult Hash(PyCallContext context)
    {
        return PyType.Hash(context, this);
    }
    public PyResult Bool(PyCallContext context)
    {
        return PyType.Bool(context, this);
    }
    public PyResult Int(PyCallContext context)
    {
        return PyType.Int(context, this);
    }
    public PyResult Float(PyCallContext context)
    {
        return PyType.Float(context, this);
    }
    public PyResult Complex(PyCallContext context)
    {
        return PyType.Complex(context, this);
    }
    public PyResult Index(PyCallContext context)
    {
        return PyType.Index(context, this);
    }
    public PyResult Len(PyCallContext context)
    {
        return PyType.Len(context, this);
    }
    public PyResult Iter(PyCallContext context)
    {
        return PyType.Iter(context, this);
    }
    public PyResult Next(PyCallContext context)
    {
        return PyType.Next(context, this);
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
    public PyResult Contains(PyCallContext context, PyObject item)
    {
        return PyType.Contains(context, this, item);
    }
    public PyResult GetItem(PyCallContext context, PyObject key)
    {
        return PyType.GetItem(context, this, key);
    }
    public PyResult SetItem(PyCallContext context, PyObject key, PyObject value)
    {
        return PyType.SetItem(context, this, key, value);
    }
    public PyResult DelItem(PyCallContext context, PyObject key)
    {
        return PyType.DelItem(context, this, key);
    }
    public PyResult Add(PyCallContext context, PyObject other)
    {
        return PyType.Add(context, this, other);
    }
    public PyResult Sub(PyCallContext context, PyObject other)
    {
        return PyType.Sub(context, this, other);
    }
    public PyResult Mul(PyCallContext context, PyObject other)
    {
        return PyType.Mul(context, this, other);
    }
    public PyResult TrueDiv(PyCallContext context, PyObject other)
    {
        return PyType.TrueDiv(context, this, other);
    }
    public PyResult FloorDiv(PyCallContext context, PyObject other)
    {
        return PyType.FloorDiv(context, this, other);
    }
    public PyResult Mod(PyCallContext context, PyObject other)
    {
        return PyType.Mod(context, this, other);
    }
    public PyResult DivMod(PyCallContext context, PyObject other)
    {
        return PyType.DivMod(context, this, other);
    }
    public PyResult Pow(PyCallContext context, PyObject other, PyObject modulo)
    {
        return PyType.Pow(context, this, other, modulo);
    }
    public PyResult LShift(PyCallContext context, PyObject other)
    {
        return PyType.LShift(context, this, other);
    }
    public PyResult RShift(PyCallContext context, PyObject other)
    {
        return PyType.RShift(context, this, other);
    }
    public PyResult And(PyCallContext context, PyObject other)
    {
        return PyType.And(context, this, other);
    }
    public PyResult Xor(PyCallContext context, PyObject other)
    {
        return PyType.Xor(context, this, other);
    }
    public PyResult Or(PyCallContext context, PyObject other)
    {
        return PyType.Or(context, this, other);
    }
    public PyResult RAdd(PyCallContext context, PyObject other)
    {
        return PyType.RAdd(context, this, other);
    }
    public PyResult RSub(PyCallContext context, PyObject other)
    {
        return PyType.RSub(context, this, other);
    }
    public PyResult RMul(PyCallContext context, PyObject other)
    {
        return PyType.RMul(context, this, other);
    }
    public PyResult RTrueDiv(PyCallContext context, PyObject other)
    {
        return PyType.RTrueDiv(context, this, other);
    }
    public PyResult RFloorDiv(PyCallContext context, PyObject other)
    {
        return PyType.RFloorDiv(context, this, other);
    }
    public PyResult RMod(PyCallContext context, PyObject other)
    {
        return PyType.RMod(context, this, other);
    }
    public PyResult RDivMod(PyCallContext context, PyObject other)
    {
        return PyType.RDivMod(context, this, other);
    }
    public PyResult RPow(PyCallContext context, PyObject other, PyObject modulo)
    {
        return PyType.RPow(context, this, other, modulo);
    }
    public PyResult RLShift(PyCallContext context, PyObject other)
    {
        return PyType.RLShift(context, this, other);
    }
    public PyResult RRShift(PyCallContext context, PyObject other)
    {
        return PyType.RRShift(context, this, other);
    }
    public PyResult RAnd(PyCallContext context, PyObject other)
    {
        return PyType.RAnd(context, this, other);
    }
    public PyResult RXor(PyCallContext context, PyObject other)
    {
        return PyType.RXor(context, this, other);
    }
    public PyResult ROr(PyCallContext context, PyObject other)
    {
        return PyType.ROr(context, this, other);
    }
    public PyResult Lt(PyCallContext context, PyObject other)
    {
        return PyType.Lt(context, this, other);
    }
    public PyResult Le(PyCallContext context, PyObject other)
    {
        return PyType.Le(context, this, other);
    }
    public PyResult Eq(PyCallContext context, PyObject other)
    {
        return PyType.Eq(context, this, other);
    }
    public PyResult Ne(PyCallContext context, PyObject other)
    {
        return PyType.Ne(context, this, other);
    }
    public PyResult Gt(PyCallContext context, PyObject other)
    {
        return PyType.Gt(context, this, other);
    }
    public PyResult Ge(PyCallContext context, PyObject other)
    {
        return PyType.Ge(context, this, other);
    }
    public PyResult Get(PyCallContext context, PyObject instance, PyObject owner)
    {
        return PyType.Get(context, this, instance, owner);
    }
    public PyResult Set(PyCallContext context, PyObject instance, PyObject value)
    {
        return PyType.Set(context, this, instance, value);
    }
    public PyResult Delete(PyCallContext context, PyObject instance)
    {
        return PyType.Delete(context, this, instance);
    }
    public PyResult SetName(PyCallContext context, PyObject owner, PyObject name)
    {
        return PyType.SetName(context, this, owner, name);
    }
    public PyResult Call(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyType.Call(context, this, args, kwargs);
    }
    public PyResult Missing(PyCallContext context, PyObject key)
    {
        return PyType.Missing(context, this, key);
    }
    public PyResult GetAttr(PyCallContext context, string name)
    {
        return PyType.GetAttr(context, this, name);
    }
    public PyResult SetAttr(PyCallContext context, string name, PyObject value)
    {
        return PyType.SetAttr(context, this, name, value);
    }
    public PyResult DelAttr(PyCallContext context, string name)
    {
        return PyType.DelAttr(context, this, name);
    }
    public PyResult Init(PyCallContext context, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyType.Init(context, this, args, kwargs);
    }
    public PyResult GetAttribute(PyCallContext context, string name)
    {
        return PyType.GetAttribute(context, this, name);
    }
    public PyResult Format(PyCallContext context, string formatSpec)
    {
        return PyType.Format(context, this, formatSpec);
    }
}

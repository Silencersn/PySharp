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
    public PyObject? Complex()
    {
        return PyType.Complex(PyCallContext.Null, this).Value;
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
    public PyObject? Abs()
    {
        return PyType.Abs(PyCallContext.Null, this).Value;
    }
    public PyObject? Neg()
    {
        return PyType.Neg(PyCallContext.Null, this).Value;
    }
    public PyObject? Pos()
    {
        return PyType.Pos(PyCallContext.Null, this).Value;
    }
    public PyObject? Invert()
    {
        return PyType.Invert(PyCallContext.Null, this).Value;
    }
    public PyObject? Contains(PyObject item)
    {
        return PyType.Contains(PyCallContext.Null, this, item).Value;
    }
    public PyObject? GetItem(PyObject key)
    {
        return PyType.GetItem(PyCallContext.Null, this, key).Value;
    }
    public PyObject? SetItem(PyObject key, PyObject value)
    {
        return PyType.SetItem(PyCallContext.Null, this, key, value).Value;
    }
    public PyObject? DelItem(PyObject key)
    {
        return PyType.DelItem(PyCallContext.Null, this, key).Value;
    }
    public PyObject? Add(PyObject other)
    {
        return PyType.Add(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Sub(PyObject other)
    {
        return PyType.Sub(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Mul(PyObject other)
    {
        return PyType.Mul(PyCallContext.Null, this, other).Value;
    }
    public PyObject? TrueDiv(PyObject other)
    {
        return PyType.TrueDiv(PyCallContext.Null, this, other).Value;
    }
    public PyObject? FloorDiv(PyObject other)
    {
        return PyType.FloorDiv(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Mod(PyObject other)
    {
        return PyType.Mod(PyCallContext.Null, this, other).Value;
    }
    public PyObject? DivMod(PyObject other)
    {
        return PyType.DivMod(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Pow(PyObject other, PyObject modulo)
    {
        return PyType.Pow(PyCallContext.Null, this, other, modulo).Value;
    }
    public PyObject? LShift(PyObject other)
    {
        return PyType.LShift(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RShift(PyObject other)
    {
        return PyType.RShift(PyCallContext.Null, this, other).Value;
    }
    public PyObject? And(PyObject other)
    {
        return PyType.And(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Xor(PyObject other)
    {
        return PyType.Xor(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Or(PyObject other)
    {
        return PyType.Or(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RAdd(PyObject other)
    {
        return PyType.RAdd(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RSub(PyObject other)
    {
        return PyType.RSub(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RMul(PyObject other)
    {
        return PyType.RMul(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RTrueDiv(PyObject other)
    {
        return PyType.RTrueDiv(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RFloorDiv(PyObject other)
    {
        return PyType.RFloorDiv(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RMod(PyObject other)
    {
        return PyType.RMod(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RDivMod(PyObject other)
    {
        return PyType.RDivMod(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RPow(PyObject other, PyObject modulo)
    {
        return PyType.RPow(PyCallContext.Null, this, other, modulo).Value;
    }
    public PyObject? RLShift(PyObject other)
    {
        return PyType.RLShift(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RRShift(PyObject other)
    {
        return PyType.RRShift(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RAnd(PyObject other)
    {
        return PyType.RAnd(PyCallContext.Null, this, other).Value;
    }
    public PyObject? RXor(PyObject other)
    {
        return PyType.RXor(PyCallContext.Null, this, other).Value;
    }
    public PyObject? ROr(PyObject other)
    {
        return PyType.ROr(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Lt(PyObject other)
    {
        return PyType.Lt(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Le(PyObject other)
    {
        return PyType.Le(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Eq(PyObject other)
    {
        return PyType.Eq(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Ne(PyObject other)
    {
        return PyType.Ne(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Gt(PyObject other)
    {
        return PyType.Gt(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Ge(PyObject other)
    {
        return PyType.Ge(PyCallContext.Null, this, other).Value;
    }
    public PyObject? Get(PyObject instance, PyObject owner)
    {
        return PyType.Get(PyCallContext.Null, this, instance, owner).Value;
    }
    public PyObject? Set(PyObject instance, PyObject value)
    {
        return PyType.Set(PyCallContext.Null, this, instance, value).Value;
    }
    public PyObject? Delete(PyObject instance)
    {
        return PyType.Delete(PyCallContext.Null, this, instance).Value;
    }
    public PyObject? SetName(PyObject owner, PyObject name)
    {
        return PyType.SetName(PyCallContext.Null, this, owner, name).Value;
    }
    public PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyType.Call(PyCallContext.Null, this, args, kwargs).Value;
    }
    public PyObject? Missing(PyObject key)
    {
        return PyType.Missing(PyCallContext.Null, this, key).Value;
    }
    public PyObject? GetAttr(string name)
    {
        return PyType.GetAttr(PyCallContext.Null, this, name).Value;
    }
    public PyObject? SetAttr(string name, PyObject value)
    {
        return PyType.SetAttr(PyCallContext.Null, this, name, value).Value;
    }
    public PyObject? DelAttr(string name)
    {
        return PyType.DelAttr(PyCallContext.Null, this, name).Value;
    }
    public PyObject? Init(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyType.Init(PyCallContext.Null, this, args, kwargs).Value;
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

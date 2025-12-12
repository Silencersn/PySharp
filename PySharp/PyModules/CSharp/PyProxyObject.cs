using PySharp.PyModules.Builtins;

namespace PySharp.PyModules.CSharp;

internal sealed class PyProxyObject : PyObject
{
    private readonly PyObject _target;

    public override PyTypeObject PyType => _target.PyType;

    public PyProxyObject(PyObject target)
    {
        _target = target;
    }

    public override PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs) => _target.Call(args, kwargs);
    public override PyObject? Repr() => _target.Repr();
    public override PyObject? GetAttribute(string item) => _target.GetAttribute(item);
    public override PyObject? GetAttr(string item) => _target.GetAttr(item);
    public override PyObject? SetAttr(string key, PyObject value) => _target.SetAttr(key, value);
    public override PyObject? DelAttr(string item) => _target.DelAttr(item);
    public override PyObject? Bool() => _target.Bool();
    public override PyObject? Int() => _target.Int();
    public override PyObject? Float() => _target.Float();
    public override PyObject? Complex() => _target.Complex();
    public override PyObject? Index() => _target.Index();
    public override PyObject? Contains(PyObject item) => _target.Contains(item);
    public override PyObject? GetItem(PyObject item) => _target.GetItem(item);
    public override PyObject? SetItem(PyObject key, PyObject value) => _target.SetItem(key, value);
    public override PyObject? DelItem(PyObject key) => _target.DelItem(key);
    public override PyObject? Len() => _target.Len();
    public override PyObject? Iter() => _target.Iter();
    public override PyObject? Next() => _target.Next();
    public override PyObject? Neg() => _target.Neg();
    public override PyObject? Pos() => _target.Pos();
    public override PyObject? Invert() => _target.Invert();
    public override PyObject? Abs() => _target.Abs();
    public override PyObject? Add(PyObject other) => _target.Add(other);
    public override PyObject? Sub(PyObject other) => _target.Sub(other);
    public override PyObject? Mul(PyObject other) => _target.Mul(other);
    public override PyObject? TrueDiv(PyObject other) => _target.TrueDiv(other);
    public override PyObject? FloorDiv(PyObject other) => _target.FloorDiv(other);
    public override PyObject? Mod(PyObject other) => _target.Mod(other);
    public override PyObject? DivMod(PyObject other) => _target.DivMod(other);
    public override PyObject? Pow(PyObject other, PyObject modulo) => _target.Pow(other, modulo);
    public override PyObject? LShift(PyObject other) => _target.LShift(other);
    public override PyObject? RShift(PyObject other) => _target.RShift(other);
    public override PyObject? And(PyObject other) => _target.And(other);
    public override PyObject? Xor(PyObject other) => _target.Xor(other);
    public override PyObject? Or(PyObject other) => _target.Or(other);
    public override PyObject? RAdd(PyObject other) => _target.RAdd(other);
    public override PyObject? RSub(PyObject other) => _target.RSub(other);
    public override PyObject? RMul(PyObject other) => _target.RMul(other);
    public override PyObject? RTrueDiv(PyObject other) => _target.RTrueDiv(other);
    public override PyObject? RFloorDiv(PyObject other) => _target.RFloorDiv(other);
    public override PyObject? RMod(PyObject other) => _target.RMod(other);
    public override PyObject? RDivMod(PyObject other) => _target.RDivMod(other);
    public override PyObject? RPow(PyObject other, PyObject modulo) => _target.RPow(other, modulo);
    public override PyObject? RLShift(PyObject other) => _target.RLShift(other);
    public override PyObject? RRShift(PyObject other) => _target.RRShift(other);
    public override PyObject? RAnd(PyObject other) => _target.RAnd(other);
    public override PyObject? RXor(PyObject other) => _target.RXor(other);
    public override PyObject? ROr(PyObject other) => _target.ROr(other);
    public override PyObject? Lt(PyObject other) => _target.Lt(other);
    public override PyObject? Le(PyObject other) => _target.Le(other);
    public override PyObject? Eq(PyObject other) => _target.Eq(other);
    public override PyObject? Ne(PyObject other) => _target.Ne(other);
    public override PyObject? Gt(PyObject other) => _target.Gt(other);
    public override PyObject? Ge(PyObject other) => _target.Ge(other);
    public override PyObject? Missing(PyObject key) => _target.Missing(key);
    public override PyObject? Get(PyObject instance, PyObject owner) => _target.Get(instance, owner);
    public override PyObject? Set(PyObject instance, PyObject value) => _target.Set(instance, value);
    public override PyObject? Delete(PyObject instance) => _target.Delete(instance);
    public override PyObject? SetName(PyObject owner, PyObject name) => _target.SetName(owner, name);
    public override PyObject? Hash() => _target.Hash();
    public override PyObject? Str() => _target.Str();
    public override PyObject? Init(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs) => _target.Init(args, kwargs);

}

internal sealed class PyProxyObjectType : PyTypeObject
{
    private readonly PyTypeObject _target;

    public PyProxyObjectType(PyTypeObject target)
    {
        _target = target;
    }

    public override string Name => _target.Name;
    public override PyTypeObject PyType => _target.PyType;
    public override IReadOnlyList<PyTypeObject> Bases => _target.Bases;
    public override string FullName => _target.FullName;
    public override string Document => _target.Document;

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _target.New(args, kwargs);
        if (obj is null)
            return null;
        return new PyProxyObject(obj);
    }

    public override PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs) => _target.Call(args, kwargs);
    public override PyObject? Repr() => _target.Repr();
    public override PyObject? GetAttribute(string item) => _target.GetAttribute(item);
    public override PyObject? GetAttr(string item) => _target.GetAttr(item);
    public override PyObject? SetAttr(string key, PyObject value) => _target.SetAttr(key, value);
    public override PyObject? DelAttr(string item) => _target.DelAttr(item);
    public override PyObject? Bool() => _target.Bool();
    public override PyObject? Int() => _target.Int();
    public override PyObject? Float() => _target.Float();
    public override PyObject? Complex() => _target.Complex();
    public override PyObject? Index() => _target.Index();
    public override PyObject? Contains(PyObject item) => _target.Contains(item);
    public override PyObject? GetItem(PyObject item) => _target.GetItem(item);
    public override PyObject? SetItem(PyObject key, PyObject value) => _target.SetItem(key, value);
    public override PyObject? DelItem(PyObject key) => _target.DelItem(key);
    public override PyObject? Len() => _target.Len();
    public override PyObject? Iter() => _target.Iter();
    public override PyObject? Next() => _target.Next();
    public override PyObject? Neg() => _target.Neg();
    public override PyObject? Pos() => _target.Pos();
    public override PyObject? Invert() => _target.Invert();
    public override PyObject? Abs() => _target.Abs();
    public override PyObject? Add(PyObject other) => _target.Add(other);
    public override PyObject? Sub(PyObject other) => _target.Sub(other);
    public override PyObject? Mul(PyObject other) => _target.Mul(other);
    public override PyObject? TrueDiv(PyObject other) => _target.TrueDiv(other);
    public override PyObject? FloorDiv(PyObject other) => _target.FloorDiv(other);
    public override PyObject? Mod(PyObject other) => _target.Mod(other);
    public override PyObject? DivMod(PyObject other) => _target.DivMod(other);
    public override PyObject? Pow(PyObject other, PyObject modulo) => _target.Pow(other, modulo);
    public override PyObject? LShift(PyObject other) => _target.LShift(other);
    public override PyObject? RShift(PyObject other) => _target.RShift(other);
    public override PyObject? And(PyObject other) => _target.And(other);
    public override PyObject? Xor(PyObject other) => _target.Xor(other);
    public override PyObject? Or(PyObject other) => _target.Or(other);
    public override PyObject? RAdd(PyObject other) => _target.RAdd(other);
    public override PyObject? RSub(PyObject other) => _target.RSub(other);
    public override PyObject? RMul(PyObject other) => _target.RMul(other);
    public override PyObject? RTrueDiv(PyObject other) => _target.RTrueDiv(other);
    public override PyObject? RFloorDiv(PyObject other) => _target.RFloorDiv(other);
    public override PyObject? RMod(PyObject other) => _target.RMod(other);
    public override PyObject? RDivMod(PyObject other) => _target.RDivMod(other);
    public override PyObject? RPow(PyObject other, PyObject modulo) => _target.RPow(other, modulo);
    public override PyObject? RLShift(PyObject other) => _target.RLShift(other);
    public override PyObject? RRShift(PyObject other) => _target.RRShift(other);
    public override PyObject? RAnd(PyObject other) => _target.RAnd(other);
    public override PyObject? RXor(PyObject other) => _target.RXor(other);
    public override PyObject? ROr(PyObject other) => _target.ROr(other);
    public override PyObject? Lt(PyObject other) => _target.Lt(other);
    public override PyObject? Le(PyObject other) => _target.Le(other);
    public override PyObject? Eq(PyObject other) => _target.Eq(other);
    public override PyObject? Ne(PyObject other) => _target.Ne(other);
    public override PyObject? Gt(PyObject other) => _target.Gt(other);
    public override PyObject? Ge(PyObject other) => _target.Ge(other);
    public override PyObject? Missing(PyObject key) => _target.Missing(key);
    public override PyObject? Get(PyObject instance, PyObject owner) => _target.Get(instance, owner);
    public override PyObject? Set(PyObject instance, PyObject value) => _target.Set(instance, value);
    public override PyObject? Delete(PyObject instance) => _target.Delete(instance);
    public override PyObject? SetName(PyObject owner, PyObject name) => _target.SetName(owner, name);
    public override PyObject? Hash() => _target.Hash();
    public override PyObject? Str() => _target.Str();
    public override PyObject? Init(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs) => _target.Init(args, kwargs);
}
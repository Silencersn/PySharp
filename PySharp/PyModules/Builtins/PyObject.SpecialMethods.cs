using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Collections.Frozen;

namespace PySharp.PyModules.Builtins;

partial class PyObject
{
    public PyObject? Repr()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Repr(PyCallContext.Null, this).Value;

        if (IsSelfDefaultType)
            return ReprImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Repr);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Str()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Str(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return StrImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Str);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Hash()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Hash(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return HashImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Hash);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Bool()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Bool(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return BoolImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Bool);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Int()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Int(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return IntImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Int);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Float()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Float(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return FloatImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Float);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Complex()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Complex(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return ComplexImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Complex);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Index()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Index(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return IndexImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Index);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Len()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Len(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return LenImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Len);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Iter()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Iter(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return IterImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Iter);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Next()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Next(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return NextImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Next);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Abs()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Abs(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return AbsImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Abs);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Neg()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Neg(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return NegImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Neg);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Pos()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Pos(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return PosImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Pos);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Invert()
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Invert(PyCallContext.Null, this).Value;
        if (IsSelfDefaultType)
            return InvertImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Invert);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Contains(PyObject item)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Contains(PyCallContext.Null, this, item).Value;
        if (IsSelfDefaultType)
            return ContainsImpl(item);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Contains);
        return callable?.Call([item], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? GetItem(PyObject key)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.GetItem(PyCallContext.Null, this, key).Value;
        if (IsSelfDefaultType)
            return GetItemImpl(key);
        var callable = PyObjectGetAttribute(this, PySpecialNames.GetItem);
        return callable?.Call([key], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? SetItem(PyObject key, PyObject value)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.SetItem(PyCallContext.Null, this, key, value).Value;
        if (IsSelfDefaultType)
            return SetItemImpl(key, value);
        var callable = PyObjectGetAttribute(this, PySpecialNames.SetItem);
        return callable?.Call([key, value], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? DelItem(PyObject key)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.DelItem(PyCallContext.Null, this, key).Value;
        if (IsSelfDefaultType)
            return DelItemImpl(key);
        var callable = PyObjectGetAttribute(this, PySpecialNames.DelItem);
        return callable?.Call([key], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Add(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Add(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return AddImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Add);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Sub(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Sub(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return SubImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Sub);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Mul(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Mul(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return MulImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Mul);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? TrueDiv(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.TrueDiv(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return TrueDivImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.TrueDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? FloorDiv(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.FloorDiv(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return FloorDivImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.FloorDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Mod(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Mod(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return ModImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Mod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? DivMod(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.DivMod(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return DivModImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.DivMod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Pow(PyObject other, PyObject modulo)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Pow(PyCallContext.Null, this, other, modulo).Value;
        if (IsSelfDefaultType)
            return PowImpl(other, modulo);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Pow);
        return callable?.Call([other, modulo], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? LShift(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.LShift(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return LShiftImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.LShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RShift(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RShift(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RShiftImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? And(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.And(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return AndImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.And);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Xor(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Xor(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return XorImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Xor);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Or(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Or(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return OrImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Or);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RAdd(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RAdd(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RAddImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RAdd);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RSub(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RSub(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RSubImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RSub);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RMul(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RMul(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RMulImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RMul);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RTrueDiv(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RTrueDiv(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RTrueDivImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RTrueDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RFloorDiv(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RFloorDiv(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RFloorDivImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RFloorDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RMod(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RMod(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RModImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RMod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RDivMod(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RDivMod(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RDivModImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RDivMod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RPow(PyObject other, PyObject modulo)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RPow(PyCallContext.Null, this, other, modulo).Value;
        if (IsSelfDefaultType)
            return RPowImpl(other, modulo);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RPow);
        return callable?.Call([other, modulo], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RLShift(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RLShift(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RLShiftImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RLShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RRShift(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RRShift(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RRShiftImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RRShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RAnd(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RAnd(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RAndImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RAnd);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RXor(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.RXor(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return RXorImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RXor);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? ROr(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.ROr(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return ROrImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.ROr);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Lt(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Lt(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return LtImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Lt);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Le(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Le(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return LeImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Le);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Eq(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Eq(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return EqImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Eq);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Ne(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Ne(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return NeImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Ne);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Gt(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Gt(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return GtImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Gt);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Ge(PyObject other)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Ge(PyCallContext.Null, this, other).Value;
        if (IsSelfDefaultType)
            return GeImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Ge);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Get(PyObject instance, PyObject owner)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Get(PyCallContext.Null, this, instance, owner).Value;
        if (IsSelfDefaultType)
            return GetImpl(instance, owner);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Get);
        return callable?.Call([instance, owner], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Set(PyObject instance, PyObject value)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Set(PyCallContext.Null, this, instance, value).Value;
        if (IsSelfDefaultType)
            return SetImpl(instance, value);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Set);
        return callable?.Call([instance, value], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Delete(PyObject instance)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Delete(PyCallContext.Null, this, instance).Value;
        if (IsSelfDefaultType)
            return DeleteImpl(instance);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Delete);
        return callable?.Call([instance], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? SetName(PyObject owner, PyObject name)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.SetName(PyCallContext.Null, this, owner, name).Value;
        if (IsSelfDefaultType)
            return SetNameImpl(owner, name);
        var callable = PyObjectGetAttribute(this, PySpecialNames.SetName);
        return callable?.Call([owner, name], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Call(PyCallContext.Null, this, args, kwargs).Value;
        if (IsSelfDefaultType)
            return CallImpl(args, kwargs);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Call);
        return callable?.Call(args, kwargs);
    }
    public PyObject? Missing(PyObject key)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Missing(PyCallContext.Null, this, key).Value;
        if (IsSelfDefaultType)
            return MissingImpl(key);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Missing);
        return callable?.Call([key], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? GetAttr(string name)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.GetAttr(PyCallContext.Null, this, name).Value;
        if (IsSelfDefaultType)
            return GetAttrImpl(name);
        var callable = PyObjectGetAttribute(this, PySpecialNames.GetAttr);
        return callable?.Call([PyStrObject.FromString(name)], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? SetAttr(string name, PyObject value)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.SetAttr(PyCallContext.Null, this, name, value).Value;
        if (IsSelfDefaultType)
            return SetAttrImpl(name, value);
        var callable = PyObjectGetAttribute(this, PySpecialNames.SetAttr);
        return callable?.Call([PyStrObject.FromString(name), value], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? DelAttr(string name)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.DelAttr(PyCallContext.Null, this, name).Value;
        if (IsSelfDefaultType)
            return DelAttrImpl(name);
        var callable = PyObjectGetAttribute(this, PySpecialNames.DelAttr);
        return callable?.Call([PyStrObject.FromString(name)], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Init(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Init(PyCallContext.Null, this, args, kwargs).Value;
        if (IsSelfDefaultType)
            return InitImpl(args, kwargs);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Init);
        return callable?.Call(args, kwargs);
    }
    public PyObject? GetAttribute(string name)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.GetAttribute(PyCallContext.Null, this, name).Value;
        if (IsSelfDefaultType)
            return GetAttributeImpl(name);
        var callable = PyObjectGetAttribute(this, PySpecialNames.GetAttribute);
        return callable?.Call([PyStrObject.FromString(name)], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Format(string formatSpec)
    {
        if (PyType.IsPyTypeObjectOfT)
            return PyType.Format(PyCallContext.Null, this, formatSpec).Value;
        if (IsSelfDefaultType)
            return FormatImpl(formatSpec);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Format);
        return callable?.Call([PyStrObject.FromString(formatSpec)], FrozenDictionary<string, PyObject>.Empty);
    }
}

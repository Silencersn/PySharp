using PySharp.PyRuntime;
using System.Collections.Frozen;

namespace PySharp.PyModules.Builtins;

partial class PyObject
{
    public PyObject? Repr()
    {
        if (IsSelfDefaultType)
            return ReprImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Repr);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Str()
    {
        if (IsSelfDefaultType)
            return StrImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Str);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Hash()
    {
        if (IsSelfDefaultType)
            return HashImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Hash);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Bool()
    {
        if (IsSelfDefaultType)
            return BoolImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Bool);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Int()
    {
        if (IsSelfDefaultType)
            return IntImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Int);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Float()
    {
        if (IsSelfDefaultType)
            return FloatImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Float);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Complex()
    {
        if (IsSelfDefaultType)
            return ComplexImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Complex);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Index()
    {
        if (IsSelfDefaultType)
            return IndexImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Index);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Len()
    {
        if (IsSelfDefaultType)
            return LenImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Len);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Iter()
    {
        if (IsSelfDefaultType)
            return IterImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Iter);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Next()
    {
        if (IsSelfDefaultType)
            return NextImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Next);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Abs()
    {
        if (IsSelfDefaultType)
            return AbsImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Abs);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Neg()
    {
        if (IsSelfDefaultType)
            return NegImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Neg);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Pos()
    {
        if (IsSelfDefaultType)
            return PosImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Pos);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Invert()
    {
        if (IsSelfDefaultType)
            return InvertImpl();
        var callable = PyObjectGetAttribute(this, PySpecialNames.Invert);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Contains(PyObject item)
    {
        if (IsSelfDefaultType)
            return ContainsImpl(item);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Contains);
        return callable?.Call([item], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? GetItem(PyObject key)
    {
        if (IsSelfDefaultType)
            return GetItemImpl(key);
        var callable = PyObjectGetAttribute(this, PySpecialNames.GetItem);
        return callable?.Call([key], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? SetItem(PyObject key, PyObject value)
    {
        if (IsSelfDefaultType)
            return SetItemImpl(key, value);
        var callable = PyObjectGetAttribute(this, PySpecialNames.SetItem);
        return callable?.Call([key, value], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? DelItem(PyObject key)
    {
        if (IsSelfDefaultType)
            return DelItemImpl(key);
        var callable = PyObjectGetAttribute(this, PySpecialNames.DelItem);
        return callable?.Call([key], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Add(PyObject other)
    {
        if (IsSelfDefaultType)
            return AddImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Add);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Sub(PyObject other)
    {
        if (IsSelfDefaultType)
            return SubImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Sub);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Mul(PyObject other)
    {
        if (IsSelfDefaultType)
            return MulImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Mul);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? TrueDiv(PyObject other)
    {
        if (IsSelfDefaultType)
            return TrueDivImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.TrueDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? FloorDiv(PyObject other)
    {
        if (IsSelfDefaultType)
            return FloorDivImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.FloorDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Mod(PyObject other)
    {
        if (IsSelfDefaultType)
            return ModImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Mod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? DivMod(PyObject other)
    {
        if (IsSelfDefaultType)
            return DivModImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.DivMod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Pow(PyObject other, PyObject modulo)
    {
        if (IsSelfDefaultType)
            return PowImpl(other, modulo);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Pow);
        return callable?.Call([other, modulo], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? LShift(PyObject other)
    {
        if (IsSelfDefaultType)
            return LShiftImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.LShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RShift(PyObject other)
    {
        if (IsSelfDefaultType)
            return RShiftImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? And(PyObject other)
    {
        if (IsSelfDefaultType)
            return AndImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.And);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Xor(PyObject other)
    {
        if (IsSelfDefaultType)
            return XorImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Xor);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Or(PyObject other)
    {
        if (IsSelfDefaultType)
            return OrImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Or);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RAdd(PyObject other)
    {
        if (IsSelfDefaultType)
            return RAddImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RAdd);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RSub(PyObject other)
    {
        if (IsSelfDefaultType)
            return RSubImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RSub);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RMul(PyObject other)
    {
        if (IsSelfDefaultType)
            return RMulImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RMul);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RTrueDiv(PyObject other)
    {
        if (IsSelfDefaultType)
            return RTrueDivImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RTrueDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RFloorDiv(PyObject other)
    {
        if (IsSelfDefaultType)
            return RFloorDivImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RFloorDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RMod(PyObject other)
    {
        if (IsSelfDefaultType)
            return RModImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RMod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RDivMod(PyObject other)
    {
        if (IsSelfDefaultType)
            return RDivModImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RDivMod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RPow(PyObject other, PyObject modulo)
    {
        if (IsSelfDefaultType)
            return RPowImpl(other, modulo);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RPow);
        return callable?.Call([other, modulo], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RLShift(PyObject other)
    {
        if (IsSelfDefaultType)
            return RLShiftImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RLShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RRShift(PyObject other)
    {
        if (IsSelfDefaultType)
            return RRShiftImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RRShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RAnd(PyObject other)
    {
        if (IsSelfDefaultType)
            return RAndImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RAnd);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? RXor(PyObject other)
    {
        if (IsSelfDefaultType)
            return RXorImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.RXor);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? ROr(PyObject other)
    {
        if (IsSelfDefaultType)
            return ROrImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.ROr);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Lt(PyObject other)
    {
        if (IsSelfDefaultType)
            return LtImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Lt);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Le(PyObject other)
    {
        if (IsSelfDefaultType)
            return LeImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Le);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Eq(PyObject other)
    {
        if (IsSelfDefaultType)
            return EqImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Eq);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Ne(PyObject other)
    {
        if (IsSelfDefaultType)
            return NeImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Ne);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Gt(PyObject other)
    {
        if (IsSelfDefaultType)
            return GtImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Gt);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Ge(PyObject other)
    {
        if (IsSelfDefaultType)
            return GeImpl(other);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Ge);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Get(PyObject instance, PyObject owner)
    {
        if (IsSelfDefaultType)
            return GetImpl(instance, owner);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Get);
        return callable?.Call([instance, owner], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Set(PyObject instance, PyObject value)
    {
        if (IsSelfDefaultType)
            return SetImpl(instance, value);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Set);
        return callable?.Call([instance, value], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Delete(PyObject instance)
    {
        if (IsSelfDefaultType)
            return DeleteImpl(instance);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Delete);
        return callable?.Call([instance], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? SetName(PyObject owner, PyObject name)
    {
        if (IsSelfDefaultType)
            return SetNameImpl(owner, name);
        var callable = PyObjectGetAttribute(this, PySpecialNames.SetName);
        return callable?.Call([owner, name], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (IsSelfDefaultType)
            return CallImpl(args, kwargs);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Call);
        return callable?.Call(args, kwargs);
    }
    public PyObject? Missing(PyObject key)
    {
        if (IsSelfDefaultType)
            return MissingImpl(key);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Missing);
        return callable?.Call([key], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? GetAttr(string name)
    {
        if (IsSelfDefaultType)
            return GetAttrImpl(name);
        var callable = PyObjectGetAttribute(this, PySpecialNames.GetAttr);
        return callable?.Call([PyStrObject.FromString(name)], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? SetAttr(string name, PyObject value)
    {
        if (IsSelfDefaultType)
            return SetAttrImpl(name, value);
        var callable = PyObjectGetAttribute(this, PySpecialNames.SetAttr);
        return callable?.Call([PyStrObject.FromString(name), value], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? DelAttr(string name)
    {
        if (IsSelfDefaultType)
            return DelAttrImpl(name);
        var callable = PyObjectGetAttribute(this, PySpecialNames.DelAttr);
        return callable?.Call([PyStrObject.FromString(name)], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Init(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (IsSelfDefaultType)
            return InitImpl(args, kwargs);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Init);
        return callable?.Call(args, kwargs);
    }
    public PyObject? GetAttribute(string name)
    {
        if (IsSelfDefaultType)
            return GetAttributeImpl(name);
        var callable = PyObjectGetAttribute(this, PySpecialNames.GetAttribute);
        return callable?.Call([PyStrObject.FromString(name)], FrozenDictionary<string, PyObject>.Empty);
    }
    public PyObject? Format(string formatSpec)
    {
        if (IsSelfDefaultType)
            return FormatImpl(formatSpec);
        var callable = PyObjectGetAttribute(this, PySpecialNames.Format);
        return callable?.Call([PyStrObject.FromString(formatSpec)], FrozenDictionary<string, PyObject>.Empty);
    }
}

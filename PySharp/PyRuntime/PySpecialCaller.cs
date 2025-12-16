using PySharp.PyModules.Builtins;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;

namespace PySharp.PyRuntime;

internal static class PySpecialCaller
{
    public static PyObject? Repr(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Repr();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Repr);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Str(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Str();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Str);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Hash(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Hash();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Hash);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Bool(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Bool();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Bool);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Int(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Int();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Int);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Float(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Float();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Float);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Complex(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Complex();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Complex);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Index(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Index();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Index);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Len(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Len();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Len);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Iter(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Iter();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Iter);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Next(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Next();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Next);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Abs(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Abs();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Abs);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Neg(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Neg();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Neg);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Pos(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Pos();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Pos);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Invert(this PyObject self)
    {
        if (self.IsSelfDefaultType)
            return self.Invert();
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Invert);
        return callable?.Call([], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Contains(this PyObject self, PyObject item)
    {
        if (self.IsSelfDefaultType)
            return self.Contains(item);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Contains);
        return callable?.Call([item], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? GetItem(this PyObject self, PyObject key)
    {
        if (self.IsSelfDefaultType)
            return self.GetItem(key);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.GetItem);
        return callable?.Call([key], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? SetItem(this PyObject self, PyObject key, PyObject value)
    {
        if (self.IsSelfDefaultType)
            return self.SetItem(key, value);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.SetItem);
        return callable?.Call([key, value], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? DelItem(this PyObject self, PyObject key)
    {
        if (self.IsSelfDefaultType)
            return self.DelItem(key);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.DelItem);
        return callable?.Call([key], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Add(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Add(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Add);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Sub(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Sub(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Sub);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Mul(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Mul(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Mul);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? TrueDiv(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.TrueDiv(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.TrueDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? FloorDiv(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.FloorDiv(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.FloorDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Mod(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Mod(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Mod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? DivMod(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.DivMod(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.DivMod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Pow(this PyObject self, PyObject other, PyObject modulo)
    {
        if (self.IsSelfDefaultType)
            return self.Pow(other, modulo);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Pow);
        return callable?.Call([other, modulo], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? LShift(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.LShift(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.LShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RShift(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RShift(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? And(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.And(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.And);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Xor(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Xor(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Xor);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Or(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Or(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Or);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RAdd(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RAdd(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RAdd);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RSub(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RSub(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RSub);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RMul(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RMul(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RMul);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RTrueDiv(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RTrueDiv(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RTrueDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RFloorDiv(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RFloorDiv(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RFloorDiv);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RMod(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RMod(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RMod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RDivMod(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RDivMod(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RDivMod);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RPow(this PyObject self, PyObject other, PyObject modulo)
    {
        if (self.IsSelfDefaultType)
            return self.RPow(other, modulo);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RPow);
        return callable?.Call([other, modulo], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RLShift(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RLShift(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RLShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RRShift(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RRShift(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RRShift);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RAnd(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RAnd(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RAnd);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? RXor(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.RXor(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.RXor);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? ROr(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.ROr(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.ROr);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Lt(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Lt(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Lt);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Le(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Le(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Le);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Eq(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Eq(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Eq);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Ne(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Ne(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Ne);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Gt(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Gt(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Gt);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Ge(this PyObject self, PyObject other)
    {
        if (self.IsSelfDefaultType)
            return self.Ge(other);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Ge);
        return callable?.Call([other], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Get(this PyObject self, PyObject instance, PyObject owner)
    {
        if (self.IsSelfDefaultType)
            return self.Get(instance, owner);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Get);
        return callable?.Call([instance, owner], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Set(this PyObject self, PyObject instance, PyObject value)
    {
        if (self.IsSelfDefaultType)
            return self.Set(instance, value);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Set);
        return callable?.Call([instance, value], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Delete(this PyObject self, PyObject instance)
    {
        if (self.IsSelfDefaultType)
            return self.Delete(instance);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Delete);
        return callable?.Call([instance], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? SetName(this PyObject self, PyObject owner, PyObject name)
    {
        if (self.IsSelfDefaultType)
            return self.SetName(owner, name);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.SetName);
        return callable?.Call([owner, name], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Call(this PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (self.IsSelfDefaultType)
            return self.Call(args, kwargs);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Call);
        return callable?.Call(args, kwargs);
    }
    public static PyObject? Missing(this PyObject self, PyObject key)
    {
        if (self.IsSelfDefaultType)
            return self.Missing(key);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Missing);
        return callable?.Call([key], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? GetAttr(this PyObject self, string name)
    {
        if (self.IsSelfDefaultType)
            return self.GetAttr(name);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.GetAttr);
        return callable?.Call([PyStrObject.FromString(name)], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? SetAttr(this PyObject self, string name, PyObject value)
    {
        if (self.IsSelfDefaultType)
            return self.SetAttr(name, value);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.SetAttr);
        return callable?.Call([PyStrObject.FromString(name), value], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? DelAttr(this PyObject self, string name)
    {
        if (self.IsSelfDefaultType)
            return self.DelAttr(name);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.DelAttr);
        return callable?.Call([PyStrObject.FromString(name)], FrozenDictionary<string, PyObject>.Empty);
    }
    public static PyObject? Init(this PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (self.IsSelfDefaultType)
            return self.Init(args, kwargs);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.Init);
        return callable?.Call(args, kwargs);
    }
    public static PyObject? GetAttribute(this PyObject self, string name)
    {
        if (self.IsSelfDefaultType)
            return self.GetAttribute(name);
        var callable = PyObject.PyObjectGetAttribute(self, PySpecialNames.GetAttribute);
        return callable?.Call([PyStrObject.FromString(name)], FrozenDictionary<string, PyObject>.Empty);
    }
}

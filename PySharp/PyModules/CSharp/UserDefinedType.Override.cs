using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace PySharp.PyModules.CSharp;

partial class UserDefinedType<TObject> : PyTypeObject<TObject> where TObject : PyObject
{
    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.New, out var method))
            return method.Call([cls, .. args], kwargs) ?? PyResult.CaptureExceptionFromPVM();
        return Bases[0].New(context, cls, args, kwargs);
    }

    protected internal override PyResult Call(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Call, out var method))
            return method.Call([self, .. args], kwargs) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Call}'.");
    }

    protected internal override PyResult Repr(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Repr, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Repr}'.");
    }

    protected internal override PyResult Init(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Init, out var method))
            return method.Call([self, .. args], kwargs) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Init}'.");
    }

    protected internal override PyResult Str(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Str, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Str}'.");
    }

    protected internal override PyResult Hash(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Hash, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Hash}'.");
    }

    protected internal override PyResult GetAttribute(PyCallContext context, TObject self, string item)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.GetAttribute, out var method))
            return method.Call([self, PyStrObject.FromString(item)], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.GetAttribute}'.");
    }

    protected internal override PyResult GetAttr(PyCallContext context, TObject self, string item)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.GetAttr, out var method))
            return method.Call([self, PyStrObject.FromString(item)], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.GetAttr}'.");
    }

    protected internal override PyResult SetAttr(PyCallContext context, TObject self, string key, PyObject value)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.SetAttr, out var method))
            return method.Call([self, PyStrObject.FromString(key), value], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.SetAttr}'.");
    }

    protected internal override PyResult DelAttr(PyCallContext context, TObject self, string item)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.DelAttr, out var method))
            return method.Call([self, PyStrObject.FromString(item)], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.DelAttr}'.");
    }

    protected internal override PyResult Bool(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Bool, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Bool}'.");
    }

    protected internal override PyResult Int(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Int, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Int}'.");
    }

    protected internal override PyResult Float(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Float, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Float}'.");
    }

    protected internal override PyResult Complex(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Complex, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Complex}'.");
    }

    protected internal override PyResult Index(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Index, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Index}'.");
    }

    protected internal override PyResult Contains(PyCallContext context, TObject self, PyObject item)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Contains, out var method))
            return method.Call([self, item], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Contains}'.");
    }

    protected internal override PyResult GetItem(PyCallContext context, TObject self, PyObject item)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.GetItem, out var method))
            return method.Call([self, item], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.GetItem}'.");
    }

    protected internal override PyResult SetItem(PyCallContext context, TObject self, PyObject key, PyObject value)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.SetItem, out var method))
            return method.Call([self, key, value], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.SetItem}'.");
    }

    protected internal override PyResult DelItem(PyCallContext context, TObject self, PyObject key)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.DelItem, out var method))
            return method.Call([self, key], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.DelItem}'.");
    }

    protected internal override PyResult Len(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Len, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Len}'.");
    }

    protected internal override PyResult Iter(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Iter, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Iter}'.");
    }

    protected internal override PyResult Next(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Next, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Next}'.");
    }

    protected internal override PyResult Neg(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Neg, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Neg}'.");
    }

    protected internal override PyResult Pos(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Pos, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Pos}'.");
    }

    protected internal override PyResult Invert(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Invert, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Invert}'.");
    }

    protected internal override PyResult Abs(PyCallContext context, TObject self)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Abs, out var method))
            return method.Call([self], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Abs}'.");
    }

    protected internal override PyResult Add(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Add, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Add}'.");
    }
    protected internal override PyResult Sub(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Sub, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Sub}'.");
    }
    protected internal override PyResult Mul(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Mul, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Mul}'.");
    }
    protected internal override PyResult TrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.TrueDiv, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.TrueDiv}'.");
    }
    protected internal override PyResult FloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.FloorDiv, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.FloorDiv}'.");
    }
    protected internal override PyResult Mod(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Mod, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Mod}'.");
    }
    protected internal override PyResult DivMod(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.DivMod, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.DivMod}'.");
    }
    protected internal override PyResult Pow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Pow, out var method))
            return method.Call([self, other, modulo], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Pow}'.");
    }
    protected internal override PyResult LShift(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.LShift, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.LShift}'.");
    }
    protected internal override PyResult RShift(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RShift, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RShift}'.");
    }
    protected internal override PyResult And(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.And, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.And}'.");
    }
    protected internal override PyResult Xor(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Xor, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Xor}'.");
    }
    protected internal override PyResult Or(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Or, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Or}'.");
    }

    protected internal override PyResult RAdd(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RAdd, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RAdd}'.");
    }
    protected internal override PyResult RSub(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RSub, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RSub}'.");
    }
    protected internal override PyResult RMul(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RMul, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RMul}'.");
    }
    protected internal override PyResult RTrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RTrueDiv, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RTrueDiv}'.");
    }
    protected internal override PyResult RFloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RFloorDiv, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RFloorDiv}'.");
    }
    protected internal override PyResult RMod(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RMod, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RMod}'.");
    }
    protected internal override PyResult RDivMod(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RDivMod, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RDivMod}'.");
    }
    protected internal override PyResult RPow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RPow, out var method))
            return method.Call([self, other, modulo], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RPow}'.");
    }
    protected internal override PyResult RLShift(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RLShift, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RLShift}'.");
    }
    protected internal override PyResult RRShift(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RRShift, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RRShift}'.");
    }
    protected internal override PyResult RAnd(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RAnd, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RAnd}'.");
    }
    protected internal override PyResult RXor(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.RXor, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.RXor}'.");
    }
    protected internal override PyResult ROr(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.ROr, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.ROr}'.");
    }

    protected internal override PyResult Lt(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Lt, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Lt}'.");
    }
    protected internal override PyResult Le(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Le, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Le}'.");
    }
    protected internal override PyResult Eq(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Eq, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Eq}'.");
    }
    protected internal override PyResult Ne(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Ne, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Ne}'.");
    }
    protected internal override PyResult Gt(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Gt, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Gt}'.");
    }
    protected internal override PyResult Ge(PyCallContext context, TObject self, PyObject other)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Ge, out var method))
            return method.Call([self, other], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Ge}'.");
    }

    protected internal override PyResult Missing(PyCallContext context, TObject self, PyObject key)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Missing, out var method))
            return method.Call([self, key], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Missing}'.");
    }

    protected internal override PyResult Get(PyCallContext context, TObject self, PyObject instance, PyObject owner)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Get, out var method))
            return method.Call([self, instance, owner], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Get}'.");
    }

    protected internal override PyResult Set(PyCallContext context, TObject self, PyObject instance, PyObject value)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Set, out var method))
            return method.Call([self, instance, value], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Set}'.");
    }

    protected internal override PyResult Delete(PyCallContext context, TObject self, PyObject instance)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Delete, out var method))
            return method.Call([self, instance], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Delete}'.");
    }

    protected internal override PyResult SetName(PyCallContext context, TObject self, PyObject owner, PyObject name)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.SetName, out var method))
            return method.Call([self, owner, name], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.SetName}'.");
    }

    protected internal override PyResult Format(PyCallContext context, TObject self, string formatSpec)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.Format, out var method))
            return method.Call([self, PyStrObject.FromString(formatSpec)], FrozenDictionary<string, PyObject>.Empty) ?? PyResult.CaptureExceptionFromPVM();
        return PyResult.RaiseAttributeError($"'{Name}' object has no attribute '{PySpecialNames.Format}'.");
    }
}
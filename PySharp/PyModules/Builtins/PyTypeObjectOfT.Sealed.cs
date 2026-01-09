using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

partial class PyTypeObject<TObject>
{
    private protected sealed override PyResult Init(PyCallContext context, PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Init}' requires a '{Name}' object but received a '{self.PyType.Name}'");

        return Init(context, selfOfT, args, kwargs);
    }

    private protected sealed override PyResult Call(PyCallContext context, PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Call}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Call(context, selfOfT, args, kwargs);
    }

    private protected sealed override PyResult Repr(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Repr}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Repr(context, selfOfT);
    }

    private protected sealed override PyResult Str(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Str}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Str(context, selfOfT);
    }

    private protected sealed override PyResult Hash(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Hash}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Hash(context, selfOfT);
    }

    private protected sealed override PyResult GetAttribute(PyCallContext context, PyObject self, PyObject item)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.GetAttribute}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return GetAttribute(context, selfOfT, item);
    }

    private protected sealed override PyResult GetAttr(PyCallContext context, PyObject self, PyObject item)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.GetAttr}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return GetAttr(context, selfOfT, item);
    }

    private protected sealed override PyResult SetAttr(PyCallContext context, PyObject self, PyObject key, PyObject value)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.SetAttr}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return SetAttr(context, selfOfT, key, value);
    }

    private protected sealed override PyResult DelAttr(PyCallContext context, PyObject self, PyObject item)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.DelAttr}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return DelAttr(context, selfOfT, item);
    }

    private protected sealed override PyResult Bool(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Bool}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Bool(context, selfOfT);
    }

    private protected sealed override PyResult Int(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Int}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Int(context, selfOfT);
    }

    private protected sealed override PyResult Float(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Float}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Float(context, selfOfT);
    }

    private protected sealed override PyResult Complex(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Complex}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Complex(context, selfOfT);
    }

    private protected sealed override PyResult Index(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Index}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Index(context, selfOfT);
    }

    private protected sealed override PyResult Contains(PyCallContext context, PyObject self, PyObject item)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Contains}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Contains(context, selfOfT, item);
    }

    private protected sealed override PyResult GetItem(PyCallContext context, PyObject self, PyObject item)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.GetItem}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return GetItem(context, selfOfT, item);
    }

    private protected sealed override PyResult SetItem(PyCallContext context, PyObject self, PyObject key, PyObject value)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.SetItem}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return SetItem(context, selfOfT, key, value);
    }

    private protected sealed override PyResult DelItem(PyCallContext context, PyObject self, PyObject key)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.DelItem}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return DelItem(context, selfOfT, key);
    }

    private protected sealed override PyResult Len(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Len}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Len(context, selfOfT);
    }

    private protected sealed override PyResult Iter(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Iter}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Iter(context, selfOfT);
    }

    private protected sealed override PyResult Next(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Next}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Next(context, selfOfT);
    }

    private protected sealed override PyResult Neg(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Neg}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Neg(context, selfOfT);
    }

    private protected sealed override PyResult Pos(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Pos}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Pos(context, selfOfT);
    }

    private protected sealed override PyResult Invert(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Invert}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Invert(context, selfOfT);
    }

    private protected sealed override PyResult Abs(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Abs}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Abs(context, selfOfT);
    }

    private protected sealed override PyResult Add(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Add}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Add(context, selfOfT, other);
    }
    private protected sealed override PyResult Sub(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Sub}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Sub(context, selfOfT, other);
    }
    private protected sealed override PyResult Mul(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Mul}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Mul(context, selfOfT, other);
    }
    private protected sealed override PyResult TrueDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.TrueDiv}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return TrueDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult FloorDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.FloorDiv}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return FloorDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult Mod(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Mod}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Mod(context, selfOfT, other);
    }
    private protected sealed override PyResult DivMod(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.DivMod}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return DivMod(context, selfOfT, other);
    }
    private protected sealed override PyResult Pow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Pow}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Pow(context, selfOfT, other, modulo);
    }
    private protected sealed override PyResult LShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.LShift}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return LShift(context, selfOfT, other);
    }
    private protected sealed override PyResult RShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RShift}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RShift(context, selfOfT, other);
    }
    private protected sealed override PyResult And(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.And}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return And(context, selfOfT, other);
    }
    private protected sealed override PyResult Xor(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Xor}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Xor(context, selfOfT, other);
    }
    private protected sealed override PyResult Or(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Or}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Or(context, selfOfT, other);
    }

    private protected sealed override PyResult RAdd(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RAdd}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RAdd(context, selfOfT, other);
    }
    private protected sealed override PyResult RSub(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RSub}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RSub(context, selfOfT, other);
    }
    private protected sealed override PyResult RMul(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RMul}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RMul(context, selfOfT, other);
    }
    private protected sealed override PyResult RTrueDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RTrueDiv}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RTrueDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult RFloorDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RFloorDiv}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RFloorDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult RMod(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RMod}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RMod(context, selfOfT, other);
    }
    private protected sealed override PyResult RDivMod(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RDivMod}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RDivMod(context, selfOfT, other);
    }
    private protected sealed override PyResult RPow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RPow}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RPow(context, selfOfT, other, modulo);
    }
    private protected sealed override PyResult RLShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RLShift}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RLShift(context, selfOfT, other);
    }
    private protected sealed override PyResult RRShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RRShift}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RRShift(context, selfOfT, other);
    }
    private protected sealed override PyResult RAnd(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RAnd}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RAnd(context, selfOfT, other);
    }
    private protected sealed override PyResult RXor(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.RXor}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return RXor(context, selfOfT, other);
    }
    private protected sealed override PyResult ROr(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.ROr}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return ROr(context, selfOfT, other);
    }

    private protected sealed override PyResult Lt(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Lt}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Lt(context, selfOfT, other);
    }
    private protected sealed override PyResult Le(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Le}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Le(context, selfOfT, other);
    }
    private protected sealed override PyResult Eq(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Eq}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Eq(context, selfOfT, other);
    }
    private protected sealed override PyResult Ne(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Ne}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Ne(context, selfOfT, other);
    }
    private protected sealed override PyResult Gt(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Gt}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Gt(context, selfOfT, other);
    }
    private protected sealed override PyResult Ge(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Ge}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Ge(context, selfOfT, other);
    }

    private protected sealed override PyResult Missing(PyCallContext context, PyObject self, PyObject key)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Missing}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Missing(context, selfOfT, key);
    }

    private protected sealed override PyResult Get(PyCallContext context, PyObject self, PyObject instance, PyObject owner)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Get}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Get(context, selfOfT, instance, owner);
    }
    private protected sealed override PyResult Set(PyCallContext context, PyObject self, PyObject instance, PyObject value)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Set}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Set(context, selfOfT, instance, value);
    }
    private protected sealed override PyResult Delete(PyCallContext context, PyObject self, PyObject instance)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Delete}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Delete(context, selfOfT, instance);
    }
    private protected sealed override PyResult SetName(PyCallContext context, PyObject self, PyObject owner, PyObject name)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.SetName}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return SetName(context, selfOfT, owner, name);
    }

    private protected sealed override PyResult Format(PyCallContext context, PyObject self, PyObject formatSpec)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.Format}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return Format(context, selfOfT, formatSpec);
    }

    private protected sealed override PyResult IAdd(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.IAdd}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return IAdd(context, selfOfT, other);
    }
    private protected sealed override PyResult ISub(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.ISub}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return ISub(context, selfOfT, other);
    }
    private protected sealed override PyResult IMul(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.IMul}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return IMul(context, selfOfT, other);
    }
    private protected sealed override PyResult IMatMul(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.IMatMul}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return IMatMul(context, selfOfT, other);
    }
    private protected sealed override PyResult ITrueDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.ITrueDiv}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return ITrueDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult IFloorDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.IFloorDiv}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return IFloorDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult IMod(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.IMod}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return IMod(context, selfOfT, other);
    }
    private protected sealed override PyResult IPow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.IPow}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return IPow(context, selfOfT, other, modulo);
    }
    private protected sealed override PyResult ILShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.ILShift}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return ILShift(context, selfOfT, other);
    }
    private protected sealed override PyResult IRShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.IRShift}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return IRShift(context, selfOfT, other);
    }
    private protected sealed override PyResult IAnd(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.IAnd}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return IAnd(context, selfOfT, other);
    }
    private protected sealed override PyResult IXor(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.IXor}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return IXor(context, selfOfT, other);
    }
    private protected sealed override PyResult IOr(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.RaiseTypeError($"'{PySpecialNames.IOr}' requires a '{Name}' object but received a '{self.PyType.Name}'");
        return IOr(context, selfOfT, other);
    }
}
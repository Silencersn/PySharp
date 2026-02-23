using PySharp.Runtime;
using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject<TObject>
{
    private protected sealed override PyResult Init(PyCallContext context, PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Init, FullName, self.PyType.FullName);
        return Init(context, selfOfT, args, kwargs);
    }

    private protected sealed override PyResult Call(PyCallContext context, PyObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Call, FullName, self.PyType.FullName);
        return Call(context, selfOfT, args, kwargs);
    }

    private protected sealed override PyResult Repr(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Repr, FullName, self.PyType.FullName);
        return Repr(context, selfOfT);
    }

    private protected sealed override PyResult Str(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Str, FullName, self.PyType.FullName);
        return Str(context, selfOfT);
    }

    private protected sealed override PyResult Hash(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Hash, FullName, self.PyType.FullName);
        return Hash(context, selfOfT);
    }

    private protected sealed override PyResult GetAttribute(PyCallContext context, PyObject self, PyObject item)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.GetAttribute, FullName, self.PyType.FullName);
        return GetAttribute(context, selfOfT, item);
    }

    private protected sealed override PyResult GetAttr(PyCallContext context, PyObject self, PyObject item)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.GetAttr, FullName, self.PyType.FullName);
        return GetAttr(context, selfOfT, item);
    }

    private protected sealed override PyResult SetAttr(PyCallContext context, PyObject self, PyObject key, PyObject value)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.SetAttr, FullName, self.PyType.FullName);
        return SetAttr(context, selfOfT, key, value);
    }

    private protected sealed override PyResult DelAttr(PyCallContext context, PyObject self, PyObject item)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.DelAttr, FullName, self.PyType.FullName);
        return DelAttr(context, selfOfT, item);
    }

    private protected sealed override PyResult Bool(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Bool, FullName, self.PyType.FullName);
        return Bool(context, selfOfT);
    }

    private protected sealed override PyResult Int(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Int, FullName, self.PyType.FullName);
        return Int(context, selfOfT);
    }

    private protected sealed override PyResult Float(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Float, FullName, self.PyType.FullName);
        return Float(context, selfOfT);
    }

    private protected sealed override PyResult Complex(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Complex, FullName, self.PyType.FullName);
        return Complex(context, selfOfT);
    }

    private protected sealed override PyResult Index(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Index, FullName, self.PyType.FullName);
        return Index(context, selfOfT);
    }

    private protected sealed override PyResult Contains(PyCallContext context, PyObject self, PyObject item)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Contains, FullName, self.PyType.FullName);
        return Contains(context, selfOfT, item);
    }

    private protected sealed override PyResult GetItem(PyCallContext context, PyObject self, PyObject item)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.GetItem, FullName, self.PyType.FullName);
        return GetItem(context, selfOfT, item);
    }

    private protected sealed override PyResult SetItem(PyCallContext context, PyObject self, PyObject key, PyObject value)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.SetItem, FullName, self.PyType.FullName);
        return SetItem(context, selfOfT, key, value);
    }

    private protected sealed override PyResult DelItem(PyCallContext context, PyObject self, PyObject key)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.DelItem, FullName, self.PyType.FullName);
        return DelItem(context, selfOfT, key);
    }

    private protected sealed override PyResult Len(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Len, FullName, self.PyType.FullName);
        return Len(context, selfOfT);
    }

    private protected sealed override PyResult Iter(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Iter, FullName, self.PyType.FullName);
        return Iter(context, selfOfT);
    }

    private protected sealed override PyResult Next(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Next, FullName, self.PyType.FullName);
        return Next(context, selfOfT);
    }

    private protected sealed override PyResult Neg(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Neg, FullName, self.PyType.FullName);
        return Neg(context, selfOfT);
    }

    private protected sealed override PyResult Pos(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Pos, FullName, self.PyType.FullName);
        return Pos(context, selfOfT);
    }

    private protected sealed override PyResult Invert(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Invert, FullName, self.PyType.FullName);
        return Invert(context, selfOfT);
    }

    private protected sealed override PyResult Abs(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Abs, FullName, self.PyType.FullName);
        return Abs(context, selfOfT);
    }

    private protected sealed override PyResult Add(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Add, FullName, self.PyType.FullName);
        return Add(context, selfOfT, other);
    }
    private protected sealed override PyResult Sub(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Sub, FullName, self.PyType.FullName);
        return Sub(context, selfOfT, other);
    }
    private protected sealed override PyResult Mul(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Mul, FullName, self.PyType.FullName);
        return Mul(context, selfOfT, other);
    }
    private protected sealed override PyResult TrueDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.TrueDiv, FullName, self.PyType.FullName);
        return TrueDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult FloorDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.FloorDiv, FullName, self.PyType.FullName);
        return FloorDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult Mod(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Mod, FullName, self.PyType.FullName);
        return Mod(context, selfOfT, other);
    }
    private protected sealed override PyResult DivMod(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.DivMod, FullName, self.PyType.FullName);
        return DivMod(context, selfOfT, other);
    }
    private protected sealed override PyResult Pow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Pow, FullName, self.PyType.FullName);
        return Pow(context, selfOfT, other, modulo);
    }
    private protected sealed override PyResult LShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.LShift, FullName, self.PyType.FullName);
        return LShift(context, selfOfT, other);
    }
    private protected sealed override PyResult RShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RShift, FullName, self.PyType.FullName);
        return RShift(context, selfOfT, other);
    }
    private protected sealed override PyResult And(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.And, FullName, self.PyType.FullName);
        return And(context, selfOfT, other);
    }
    private protected sealed override PyResult Xor(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Xor, FullName, self.PyType.FullName);
        return Xor(context, selfOfT, other);
    }
    private protected sealed override PyResult Or(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Or, FullName, self.PyType.FullName);
        return Or(context, selfOfT, other);
    }

    private protected sealed override PyResult RAdd(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RAdd, FullName, self.PyType.FullName);
        return RAdd(context, selfOfT, other);
    }
    private protected sealed override PyResult RSub(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RSub, FullName, self.PyType.FullName);
        return RSub(context, selfOfT, other);
    }
    private protected sealed override PyResult RMul(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RMul, FullName, self.PyType.FullName);
        return RMul(context, selfOfT, other);
    }
    private protected sealed override PyResult RTrueDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RTrueDiv, FullName, self.PyType.FullName);
        return RTrueDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult RFloorDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RFloorDiv, FullName, self.PyType.FullName);
        return RFloorDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult RMod(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RMod, FullName, self.PyType.FullName);
        return RMod(context, selfOfT, other);
    }
    private protected sealed override PyResult RDivMod(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RDivMod, FullName, self.PyType.FullName);
        return RDivMod(context, selfOfT, other);
    }
    private protected sealed override PyResult RPow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RPow, FullName, self.PyType.FullName);
        return RPow(context, selfOfT, other, modulo);
    }
    private protected sealed override PyResult RLShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RLShift, FullName, self.PyType.FullName);
        return RLShift(context, selfOfT, other);
    }
    private protected sealed override PyResult RRShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RRShift, FullName, self.PyType.FullName);
        return RRShift(context, selfOfT, other);
    }
    private protected sealed override PyResult RAnd(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RAnd, FullName, self.PyType.FullName);
        return RAnd(context, selfOfT, other);
    }
    private protected sealed override PyResult RXor(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.RXor, FullName, self.PyType.FullName);
        return RXor(context, selfOfT, other);
    }
    private protected sealed override PyResult ROr(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.ROr, FullName, self.PyType.FullName);
        return ROr(context, selfOfT, other);
    }

    private protected sealed override PyResult Lt(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Lt, FullName, self.PyType.FullName);
        return Lt(context, selfOfT, other);
    }
    private protected sealed override PyResult Le(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Le, FullName, self.PyType.FullName);
        return Le(context, selfOfT, other);
    }
    private protected sealed override PyResult Eq(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Eq, FullName, self.PyType.FullName);
        return Eq(context, selfOfT, other);
    }
    private protected sealed override PyResult Ne(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Ne, FullName, self.PyType.FullName);
        return Ne(context, selfOfT, other);
    }
    private protected sealed override PyResult Gt(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Gt, FullName, self.PyType.FullName);
        return Gt(context, selfOfT, other);
    }
    private protected sealed override PyResult Ge(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Ge, FullName, self.PyType.FullName);
        return Ge(context, selfOfT, other);
    }

    private protected sealed override PyResult Missing(PyCallContext context, PyObject self, PyObject key)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Missing, FullName, self.PyType.FullName);
        return Missing(context, selfOfT, key);
    }

    private protected sealed override PyResult Get(PyCallContext context, PyObject self, PyObject instance, PyObject owner)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Get, FullName, self.PyType.FullName);
        return Get(context, selfOfT, instance, owner);
    }
    private protected sealed override PyResult Set(PyCallContext context, PyObject self, PyObject instance, PyObject value)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Set, FullName, self.PyType.FullName);
        return Set(context, selfOfT, instance, value);
    }
    private protected sealed override PyResult Delete(PyCallContext context, PyObject self, PyObject instance)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Delete, FullName, self.PyType.FullName);
        return Delete(context, selfOfT, instance);
    }
    private protected sealed override PyResult SetName(PyCallContext context, PyObject self, PyObject owner, PyObject name)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.SetName, FullName, self.PyType.FullName);
        return SetName(context, selfOfT, owner, name);
    }

    private protected sealed override PyResult Format(PyCallContext context, PyObject self, PyObject formatSpec)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Format, FullName, self.PyType.FullName);
        return Format(context, selfOfT, formatSpec);
    }

    private protected sealed override PyResult IAdd(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.IAdd, FullName, self.PyType.FullName);
        return IAdd(context, selfOfT, other);
    }
    private protected sealed override PyResult ISub(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.ISub, FullName, self.PyType.FullName);
        return ISub(context, selfOfT, other);
    }
    private protected sealed override PyResult IMul(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.IMul, FullName, self.PyType.FullName);
        return IMul(context, selfOfT, other);
    }
    private protected sealed override PyResult IMatMul(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.IMatMul, FullName, self.PyType.FullName);
        return IMatMul(context, selfOfT, other);
    }
    private protected sealed override PyResult ITrueDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.ITrueDiv, FullName, self.PyType.FullName);
        return ITrueDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult IFloorDiv(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.IFloorDiv, FullName, self.PyType.FullName);
        return IFloorDiv(context, selfOfT, other);
    }
    private protected sealed override PyResult IMod(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.IMod, FullName, self.PyType.FullName);
        return IMod(context, selfOfT, other);
    }
    private protected sealed override PyResult IPow(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.IPow, FullName, self.PyType.FullName);
        return IPow(context, selfOfT, other, modulo);
    }
    private protected sealed override PyResult ILShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.ILShift, FullName, self.PyType.FullName);
        return ILShift(context, selfOfT, other);
    }
    private protected sealed override PyResult IRShift(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.IRShift, FullName, self.PyType.FullName);
        return IRShift(context, selfOfT, other);
    }
    private protected sealed override PyResult IAnd(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.IAnd, FullName, self.PyType.FullName);
        return IAnd(context, selfOfT, other);
    }
    private protected sealed override PyResult IXor(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.IXor, FullName, self.PyType.FullName);
        return IXor(context, selfOfT, other);
    }
    private protected sealed override PyResult IOr(PyCallContext context, PyObject self, PyObject other)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.IOr, FullName, self.PyType.FullName);
        return IOr(context, selfOfT, other);
    }
    private protected sealed override PyResult Enter(PyCallContext context, PyObject self)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Enter, FullName, self.PyType.FullName);
        return Enter(context, selfOfT);
    }
    private protected sealed override PyResult Exit(PyCallContext context, PyObject self, PyObject excType, PyObject excVal, PyObject excTb)
    {
        if (self is not TObject selfOfT)
            return PyResult.TypeError(PySR.Runtime_Type_MethodReceiveSelfWithWrongType, PySpecialNames.Exit, FullName, self.PyType.FullName);
        return Exit(context, selfOfT, excType, excVal, excTb);
    }
}
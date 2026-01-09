using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

partial class PyTypeObject<TObject>
{
    protected virtual PyResult Init(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return DefaultInit(context, self, args, kwargs);
    }

    protected virtual PyResult Call(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        throw new NotImplementedException($"{PySpecialNames.Call} does not have default implementation");
    }

    protected virtual PyResult Repr(PyCallContext context, TObject self)
    {
        return DefaultRepr(context, self);
    }

    protected virtual PyResult Str(PyCallContext context, TObject self)
    {
        return DefaultStr(context, self);
    }

    protected virtual PyResult Hash(PyCallContext context, TObject self)
    {
        return DefaultHash(context, self);
    }

    protected virtual PyResult GetAttribute(PyCallContext context, TObject self, PyObject item)
    {
        return DefaultGetAttribute(context, self, item);
    }

    protected virtual PyResult GetAttr(PyCallContext context, TObject self, PyObject item)
    {
        throw new NotImplementedException($"{PySpecialNames.GetAttr} does not have default implementation");
    }

    protected virtual PyResult SetAttr(PyCallContext context, TObject self, PyObject key, PyObject value)
    {
        // TODO: how to define immutable type
        if (self.IsImmutable)
            return PyResult.RaiseTypeError($"cannot set '{key}' attribute of immutable type '{Name}'");

        return DefaultSetAttr(context, self, key, value);
    }

    protected virtual PyResult DelAttr(PyCallContext context, TObject self, PyObject item)
    {
        // TODO: how to define immutable type
        if (self.IsImmutable)
            return PyResult.RaiseTypeError($"cannot set '{item}' attribute of immutable type '{Name}'");

        return DefaultDelAttr(context, self, item);
    }

    protected virtual PyResult Bool(PyCallContext context, TObject self)
    {
        return DefaultBool(context, self);
    }

    protected virtual PyResult Int(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Int} does not have default implementation");
    }
    protected virtual PyResult Float(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Float} does not have default implementation");
    }
    protected virtual PyResult Complex(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Complex} does not have default implementation");
    }

    protected virtual PyResult Index(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Index} does not have default implementation");
    }

    protected virtual PyResult Contains(PyCallContext context, TObject self, PyObject item)
    {
        throw new NotImplementedException($"{PySpecialNames.Contains} does not have default implementation");
    }

    protected virtual PyResult GetItem(PyCallContext context, TObject self, PyObject item)
    {
        throw new NotImplementedException($"{PySpecialNames.GetItem} does not have default implementation");
    }

    protected virtual PyResult SetItem(PyCallContext context, TObject self, PyObject key, PyObject value)
    {
        throw new NotImplementedException($"{PySpecialNames.SetItem} does not have default implementation");
    }

    protected virtual PyResult DelItem(PyCallContext context, TObject self, PyObject key)
    {
        throw new NotImplementedException($"{PySpecialNames.DelItem} does not have default implementation");
    }

    protected virtual PyResult Len(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Len} does not have default implementation");
    }

    protected virtual PyResult Iter(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Iter} does not have default implementation");
    }

    protected virtual PyResult Next(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Next} does not have default implementation");
    }

    protected virtual PyResult Neg(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Neg} does not have default implementation");
    }

    protected virtual PyResult Pos(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Pos} does not have default implementation");
    }

    protected virtual PyResult Invert(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Invert} does not have default implementation");
    }

    protected virtual PyResult Abs(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Abs} does not have default implementation");
    }

    protected virtual PyResult Add(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult Sub(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult Mul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult TrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult FloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult Mod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult DivMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult Pow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult LShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult And(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult Xor(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult Or(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }

    protected virtual PyResult RAdd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RSub(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RMul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RTrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RFloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RDivMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RPow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RLShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RRShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RAnd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult RXor(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult ROr(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }

    protected virtual PyResult Lt(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult Le(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult Eq(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultEq(context, self, other);
    }
    protected virtual PyResult Ne(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultNe(context, self, other);
    }
    protected virtual PyResult Gt(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult Ge(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }

    protected virtual PyResult Missing(PyCallContext context, TObject self, PyObject key)
    {
        throw new NotImplementedException($"{PySpecialNames.Missing} does not have default implementation");
    }

    protected virtual PyResult Get(PyCallContext context, TObject self, PyObject instance, PyObject owner)
    {
        throw new NotImplementedException($"{PySpecialNames.Get} does not have default implementation");
    }

    protected virtual PyResult Set(PyCallContext context, TObject self, PyObject instance, PyObject value)
    {
        throw new NotImplementedException($"{PySpecialNames.Set} does not have default implementation");
    }

    protected virtual PyResult Delete(PyCallContext context, TObject self, PyObject instance)
    {
        throw new NotImplementedException($"{PySpecialNames.Delete} does not have default implementation");
    }

    protected virtual PyResult SetName(PyCallContext context, TObject self, PyObject owner, PyObject name)
    {
        throw new NotImplementedException($"{PySpecialNames.SetName} does not have default implementation");
    }

    protected virtual PyResult Format(PyCallContext context, TObject self, PyObject formatSpec)
    {
        return DefaultFormat(context, self, formatSpec);
    }

    protected virtual PyResult IAdd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult ISub(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult IMul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult IMatMul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult ITrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult IFloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult IMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult IPow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult ILShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult IRShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult IAnd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult IXor(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    protected virtual PyResult IOr(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
}

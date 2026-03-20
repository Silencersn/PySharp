using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject<TObject>
{
    [PySlot]
    protected virtual PyResult Init(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return DefaultInit(context, self, args, kwargs);
    }

    [PySlot]
    protected virtual PyResult Call(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        throw new NotImplementedException($"{PySpecialNames.Call} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Repr(PyCallContext context, TObject self)
    {
        return DefaultRepr(context, self);
    }

    [PySlot]
    protected virtual PyResult Str(PyCallContext context, TObject self)
    {
        return DefaultStr(context, self);
    }

    [PySlot]
    protected virtual PyResult Hash(PyCallContext context, TObject self)
    {
        return DefaultHash(context, self);
    }

    [PySlot]
    protected virtual PyResult GetAttribute(PyCallContext context, TObject self, PyObject item)
    {
        return DefaultGetAttribute(context, self, item);
    }

    [PySlot]
    protected virtual PyResult GetAttr(PyCallContext context, TObject self, PyObject item)
    {
        throw new NotImplementedException($"{PySpecialNames.GetAttr} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult SetAttr(PyCallContext context, TObject self, PyObject key, PyObject value)
    {
        // TODO: how to define immutable type
        if (self.IsImmutable)
        {
            if (key is not PyStrObject str)
                return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, key.PyType.FullName);

            return PyResult.TypeError(PySR.Runtime_Type_SetImmutable, str.Value, self.PyType.FullName);
        }

        return DefaultSetAttr(context, self, key, value);
    }

    [PySlot]
    protected virtual PyResult DelAttr(PyCallContext context, TObject self, PyObject item)
    {
        // TODO: how to define immutable type
        if (self.IsImmutable)
        {
            if (item is not PyStrObject str)
                return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.FullName);

            return PyResult.TypeError(PySR.Runtime_Type_SetImmutable, str.Value, self.PyType.FullName);
        }
        return DefaultDelAttr(context, self, item);
    }

    [PySlot]
    protected virtual PyResult Bool(PyCallContext context, TObject self)
    {
        return DefaultBool(context, self);
    }

    [PySlot]
    protected virtual PyResult Int(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Int} does not have default implementation");
    }
    [PySlot]
    protected virtual PyResult Float(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Float} does not have default implementation");
    }
    [PySlot]
    protected virtual PyResult Complex(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Complex} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Index(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Index} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Contains(PyCallContext context, TObject self, PyObject item)
    {
        throw new NotImplementedException($"{PySpecialNames.Contains} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult GetItem(PyCallContext context, TObject self, PyObject item)
    {
        throw new NotImplementedException($"{PySpecialNames.GetItem} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult SetItem(PyCallContext context, TObject self, PyObject key, PyObject value)
    {
        throw new NotImplementedException($"{PySpecialNames.SetItem} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult DelItem(PyCallContext context, TObject self, PyObject key)
    {
        throw new NotImplementedException($"{PySpecialNames.DelItem} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Len(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Len} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Iter(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Iter} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Next(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Next} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Neg(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Neg} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Pos(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Pos} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Invert(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Invert} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Abs(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Abs} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Add(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Sub(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Mul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult TrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult FloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Mod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult DivMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Pow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult LShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult And(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Xor(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Or(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }

    [PySlot]
    protected virtual PyResult RAdd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RSub(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RMul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RTrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RFloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RDivMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RPow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RLShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RRShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RAnd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult RXor(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult ROr(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }

    [PySlot]
    protected virtual PyResult Lt(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Le(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Eq(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultEq(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Ne(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultNe(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Gt(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Ge(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }

    [PySlot]
    protected virtual PyResult Missing(PyCallContext context, TObject self, PyObject key)
    {
        throw new NotImplementedException($"{PySpecialNames.Missing} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Get(PyCallContext context, TObject self, PyObject instance, PyObject owner)
    {
        throw new NotImplementedException($"{PySpecialNames.Get} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Set(PyCallContext context, TObject self, PyObject instance, PyObject value)
    {
        throw new NotImplementedException($"{PySpecialNames.Set} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Delete(PyCallContext context, TObject self, PyObject instance)
    {
        throw new NotImplementedException($"{PySpecialNames.Delete} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult SetName(PyCallContext context, TObject self, PyObject owner, PyObject name)
    {
        throw new NotImplementedException($"{PySpecialNames.SetName} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Format(PyCallContext context, TObject self, PyObject formatSpec)
    {
        return DefaultFormat(context, self, formatSpec);
    }

    [PySlot]
    protected virtual PyResult IAdd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult ISub(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult IMul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult IMatMul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult ITrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult IFloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult IMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult IPow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult ILShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult IRShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult IAnd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult IXor(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult IOr(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual PyResult Enter(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Enter} does not have default implementation");
    }
    [PySlot]
    protected virtual PyResult Exit(PyCallContext context, TObject self, PyObject excType, PyObject excVal, PyObject excTb)
    {
        throw new NotImplementedException($"{PySpecialNames.Exit} does not have default implementation");
    }

    [PySlot]
    protected virtual PyResult Await(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Await} does not have default implementation");
    }
}

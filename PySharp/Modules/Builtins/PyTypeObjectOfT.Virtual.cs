using PySharp.Runtime;
using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject<TObject>
{
    [PySlot]
    protected virtual partial PyResult Init(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return DefaultInit(context, self, args, kwargs);
    }

    [PySlot]
    protected virtual partial PyResult Call(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        throw new NotImplementedException($"{PySpecialNames.Call} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Repr(PyCallContext context, TObject self)
    {
        return DefaultRepr(context, self);
    }

    [PySlot]
    protected virtual partial PyResult Str(PyCallContext context, TObject self)
    {
        return DefaultStr(context, self);
    }

    [PySlot]
    protected virtual partial PyResult Hash(PyCallContext context, TObject self)
    {
        return DefaultHash(context, self);
    }

    [PySlot]
    protected virtual partial PyResult GetAttribute(PyCallContext context, TObject self, PyObject item)
    {
        return DefaultGetAttribute(context, self, item);
    }

    [PySlot]
    protected virtual partial PyResult GetAttr(PyCallContext context, TObject self, PyObject item)
    {
        throw new NotImplementedException($"{PySpecialNames.GetAttr} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult SetAttr(PyCallContext context, TObject self, PyObject key, PyObject value)
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
    protected virtual partial PyResult DelAttr(PyCallContext context, TObject self, PyObject item)
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

    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Bool(PyCallContext context, TObject self)
    {
        return DefaultBool(context, self);
    }

    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Int(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Int} does not have default implementation");
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Float(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Float} does not have default implementation");
    }
    [PySlot]
    protected virtual partial PyResult Complex(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Complex} does not have default implementation");
    }

    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Index(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Index} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Contains(PyCallContext context, TObject self, PyObject item)
    {
        throw new NotImplementedException($"{PySpecialNames.Contains} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult GetItem(PyCallContext context, TObject self, PyObject item)
    {
        throw new NotImplementedException($"{PySpecialNames.GetItem} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult SetItem(PyCallContext context, TObject self, PyObject key, PyObject value)
    {
        throw new NotImplementedException($"{PySpecialNames.SetItem} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult DelItem(PyCallContext context, TObject self, PyObject key)
    {
        throw new NotImplementedException($"{PySpecialNames.DelItem} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Len(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Len} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Iter(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Iter} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Next(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Next} does not have default implementation");
    }

    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Neg(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Neg} does not have default implementation");
    }

    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Pos(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Pos} does not have default implementation");
    }

    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Invert(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Invert} does not have default implementation");
    }

    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Abs(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Abs} does not have default implementation");
    }

    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Add(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Sub(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Mul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult MatMul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult TrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult FloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Mod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult DivMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Pow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult LShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult And(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Xor(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult Or(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }

    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RAdd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RSub(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RMul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RMatMul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RTrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RFloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RDivMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RPow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RLShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RRShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RAnd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult RXor(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult ROr(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }

    [PySlot]
    protected virtual partial PyResult Lt(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual partial PyResult Le(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual partial PyResult Eq(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultEq(context, self, other);
    }
    [PySlot]
    protected virtual partial PyResult Ne(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultNe(context, self, other);
    }
    [PySlot]
    protected virtual partial PyResult Gt(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual partial PyResult Ge(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }

    [PySlot]
    protected virtual partial PyResult Missing(PyCallContext context, TObject self, PyObject key)
    {
        throw new NotImplementedException($"{PySpecialNames.Missing} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Get(PyCallContext context, TObject self, PyObject instance, PyObject owner)
    {
        throw new NotImplementedException($"{PySpecialNames.Get} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Set(PyCallContext context, TObject self, PyObject instance, PyObject value)
    {
        throw new NotImplementedException($"{PySpecialNames.Set} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Delete(PyCallContext context, TObject self, PyObject instance)
    {
        throw new NotImplementedException($"{PySpecialNames.Delete} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult SetName(PyCallContext context, TObject self, PyObject owner, PyObject name)
    {
        throw new NotImplementedException($"{PySpecialNames.SetName} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Format(PyCallContext context, TObject self, PyObject formatSpec)
    {
        return DefaultFormat(context, self, formatSpec);
    }

    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult IAdd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult ISub(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult IMul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult IMatMul(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult ITrueDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult IFloorDiv(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult IMod(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult IPow(PyCallContext context, TObject self, PyObject other, PyObject modulo)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult ILShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult IRShift(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult IAnd(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult IXor(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot(SlotsMember = nameof(PyTypeSlots.Number))]
    protected virtual partial PyResult IOr(PyCallContext context, TObject self, PyObject other)
    {
        return DefaultBinaryOperator(context, self, other);
    }
    [PySlot]
    protected virtual partial PyResult Enter(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Enter} does not have default implementation");
    }
    [PySlot]
    protected virtual partial PyResult Exit(PyCallContext context, TObject self, PyObject excType, PyObject excVal, PyObject excTb)
    {
        throw new NotImplementedException($"{PySpecialNames.Exit} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Await(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Await} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult AIter(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.AIter} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult ANext(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.ANext} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult AEnter(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.AEnter} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult AExit(PyCallContext context, TObject self, PyObject excType, PyObject excVal, PyObject excTb)
    {
        throw new NotImplementedException($"{PySpecialNames.AExit} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Reversed(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Reversed} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Round(PyCallContext context, TObject self, PyObject ndigits)
    {
        throw new NotImplementedException($"{PySpecialNames.Round} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Trunc(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Trunc} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Floor(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Floor} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Ceil(PyCallContext context, TObject self)
    {
        throw new NotImplementedException($"{PySpecialNames.Ceil} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult Buffer(PyCallContext context, TObject self, int flags)
    {
        throw new NotImplementedException($"{PySpecialNames.Buffer} does not have default implementation");
    }

    [PySlot]
    protected virtual partial PyResult ReleaseBuffer(PyCallContext context, TObject self, PyObject buffer)
    {
        throw new NotImplementedException($"{PySpecialNames.ReleaseBuffer} does not have default implementation");
    }
}
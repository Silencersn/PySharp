using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using PySharp.Utility;
using System.Diagnostics;
using System.Numerics;

namespace PySharp.Modules.Builtins;

public class PyIntObject : PyObject
{
    internal const int NegativePoolSize = 6;
    internal const int PositivesPoolSize = 257;

    internal static readonly PyIntObject[] NegativeInts;
    internal static readonly PyIntObject[] PositiveInts;

    public static PyIntObject Zero { get; }
    public static PyIntObject One { get; }
    public static PyIntObject MinusOne { get; }

    static PyIntObject()
    {
        NegativeInts = new PyIntObject[NegativePoolSize];
        PositiveInts = new PyIntObject[PositivesPoolSize];

        for (int i = 0; i < NegativeInts.Length; i++)
        {
            NegativeInts[i] = new PyIntObject(-i);
        }
        for (int i = 0; i < PositiveInts.Length; i++)
        {
            PositiveInts[i] = new PyIntObject(i);
        }

        Zero = PositiveInts[0];
        One = PositiveInts[1];
        MinusOne = NegativeInts[1];
    }

    public override PyTypeObject DefaultPyType => PyIntObjectType.Shared;

    public BigInteger Value { get; internal set; }
    public int Int32Value => (int)Value;

    internal PyIntObject(BigInteger value)
    {
        Value = value;
    }


    public static PyIntObject FromInteger(int value)
    {
        if (value < PositivesPoolSize)
        {
            if (value >= 0)
                return PositiveInts[value];

            if (value > -NegativePoolSize)
                return NegativeInts[-value];
        }

        return new PyIntObject(value);
    }
    public static PyIntObject FromInteger(long value)
    {
        if (value < PositivesPoolSize)
        {
            if (value >= 0)
                return PositiveInts[value];

            if (value > -NegativePoolSize)
                return NegativeInts[-value];
        }

        return new PyIntObject(value);
    }
    public static PyIntObject FromInteger(BigInteger value)
    {
        if (value < PositivesPoolSize)
        {
            if (value >= 0)
                return PositiveInts[(int)value];

            if (value > -NegativePoolSize)
                return NegativeInts[-(int)value];
        }

        return new PyIntObject(value);
    }
    public static PyIntObject FromIntegerNoCache(BigInteger value)
    {
        return new PyIntObject(value);
    }
}

[PyType("int")]
public sealed partial class PyIntObjectType : PyTypeObject<PyIntObjectType, PyIntObject>
{
    public override string Name => "int";

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionArgsDef("number=0", "/")]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        if (arguments.Args[0] is PyStrObject str)
        {
            if (!BigIntegerHelper.TryParse(str.Value, 10, out var integer))
                return PyResult.ValueError(PySR.Runtime_Number_Int_InvalidLiteral, 10, str.Value);

            return PyIntObject.FromInteger(integer);
        }

        var result = PyNumber.Int(context, arguments[0]);
        if (result.IsError)
            return result;

        return result.Value;
    }
    [PyFunctionArgsDef("string", "/", "base=10")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        if (arguments.Args[1] is not PyIntObject numBase)
            return PyResult.TypeError(PySR.Runtime_Number_Int_CannotInterpretedAsInt, arguments.Args[1].PyType.FullName);

        if (!((numBase.Value >= 2 && numBase.Value <= 36) || numBase.Value.IsZero))
            return PyResult.ValueError(PySR.Runtime_Number_Int_BaseOutOfRange);

        if (arguments.Args[0] is PyStrObject str)
        {
            if (!BigIntegerHelper.TryParse(str.Value, numBase.Int32Value, out var result))
                return PyResult.ValueError(PySR.Runtime_Number_Int_InvalidLiteral, numBase.Value, str.Value);

            return PyIntObject.FromInteger(result);
        }

        return PyResult.TypeError(PySR.Runtime_Number_Int_ConvertNonStr);

    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var result = _new.Call(context, args, kwargs);
        if (result.IsError)
            return result;

        var obj = result.Value;
        Debug.Assert(obj is PyIntObject);
        var value = ((PyIntObject)obj).Value;

        if (!PyObjectComparer.Default.Equals(cls, this) && value > -PyIntObject.NegativePoolSize && value < PyIntObject.PositivesPoolSize)
            obj = PyIntObject.FromIntegerNoCache(value);

        obj._pyType = cls;
        return obj;
    }

    protected override PyResult Index(PyCallContext context, PyIntObject self)
    {
        return self;
    }

    protected override PyResult Hash(PyCallContext context, PyIntObject self)
    {
        return self;
    }

    protected override PyResult Repr(PyCallContext context, PyIntObject self)
    {
        return PyStrObject.FromString(self.Value.ToString());
    }

    protected override PyResult Bool(PyCallContext context, PyIntObject self)
    {
        return PyBoolObject.FromBoolean(self.Value != 0);
    }

    protected override PyResult Int(PyCallContext context, PyIntObject self)
    {
        return self;
    }

    protected override PyResult Float(PyCallContext context, PyIntObject self)
    {
        return PyFloatObject.FromDouble((double)self.Value);
    }

    protected override PyResult Neg(PyCallContext context, PyIntObject self)
    {
        return PyIntObject.FromInteger(-self.Value);
    }

    protected override PyResult Pos(PyCallContext context, PyIntObject self)
    {
        return self;
    }

    protected override PyResult Abs(PyCallContext context, PyIntObject self)
    {
        return self.Value >= 0 ? self : PyIntObject.FromInteger(-self.Value);
    }

    protected override PyResult Invert(PyCallContext context, PyIntObject self)
    {
        return PyIntObject.FromInteger(~self.Value);
    }

    protected override PyResult Add(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Add(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Add, self, intObj);
    }
    protected override PyResult Sub(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Sub(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Sub, self, intObj);
    }
    protected override PyResult Mul(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Mul(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Mult, self, intObj);
    }
    protected override PyResult TrueDiv(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.TrueDiv(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.TrueDiv, self, intObj);
    }
    protected override PyResult FloorDiv(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.FloorDiv(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.FloorDiv, self, intObj);
    }
    protected override PyResult Mod(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Mod(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Mod, self, intObj);
    }
    protected override PyResult DivMod(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.DivMod(context, self, other);
        var (q, r) = BigInteger.DivRem(self.Value, intObj.Value);
        return PyTupleObject.CreateTuple(PyIntObject.FromInteger(q), PyIntObject.FromInteger(r));
    }
    protected override PyResult Pow(PyCallContext context, PyIntObject self, PyObject other, PyObject modulo)
    {
        if (other is not PyIntObject intObj)
            return base.Pow(context, self, other, modulo);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Pow, self, intObj, modulo);
    }
    protected override PyResult LShift(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.LShift(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.LShift, self, intObj);
    }
    protected override PyResult RShift(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.RShift(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.RShift, self, intObj);
    }
    protected override PyResult And(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.And(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.BitAnd, self, intObj);
    }
    protected override PyResult Xor(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Xor(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.BitXor, self, intObj);
    }
    protected override PyResult Or(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Or(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.BitOr, self, intObj);
    }
    protected override PyResult Lt(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Lt(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Lt, self, intObj);
    }
    protected override PyResult Gt(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Gt(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Gt, self, intObj);
    }
    protected override PyResult Le(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Le(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.LtE, self, intObj);
    }
    protected override PyResult Ge(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Ge(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.GtE, self, intObj);
    }
    protected override PyResult Eq(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Eq(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Eq, self, intObj);
    }
}
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace PySharp.PyModules.Builtins;

public class PyIntObject : PyObject
{
    internal const int NegativePoolSize = 6;
    internal const int PositivesPoolSize = 257;

    private static readonly PyIntObject[] _negatives;
    private static readonly PyIntObject[] _positives;
    public static PyIntObject Zero { get; }
    public static PyIntObject One { get; }
    public static PyIntObject MinusOne { get; }

    static PyIntObject()
    {
        _negatives = new PyIntObject[NegativePoolSize];
        _positives = new PyIntObject[PositivesPoolSize];

        for (int i = 0; i < _negatives.Length; i++)
        {
            _negatives[i] = new PyIntObject(-i);
        }
        for (int i = 0; i < _positives.Length; i++)
        {
            _positives[i] = new PyIntObject(i);
        }

        Zero = _positives[0];
        One = _positives[1];
        MinusOne = _negatives[1];
    }

    public override PyTypeObject DefaultPyType => PyIntObjectType.Shared;

    public BigInteger Value { get; internal set; }
    public int Int32Value => (int)Value;
    public int UncheckedInt32Value => unchecked((int)Value);

    internal PyIntObject(BigInteger value)
    {
        Value = value;
    }


    public static PyIntObject FromInteger(int value)
    {
        if (value < PositivesPoolSize)
        {
            if (value >= 0)
                return _positives[value];

            if (value > -NegativePoolSize)
                return _negatives[-value];
        }

        return new PyIntObject(value);
    }
    public static PyIntObject FromInteger(long value)
    {
        if (value < PositivesPoolSize)
        {
            if (value >= 0)
                return _positives[value];

            if (value > -NegativePoolSize)
                return _negatives[-value];
        }

        return new PyIntObject(value);
    }
    public static PyIntObject FromInteger(BigInteger value)
    {
        if (value < PositivesPoolSize)
        {
            if (value >= 0)
                return _positives[(int)value];

            if (value > -NegativePoolSize)
                return _negatives[-(int)value];
        }

        return new PyIntObject(value);
    }
    public static PyIntObject FromIntegerNoCache(BigInteger value)
    {
        return new PyIntObject(value);
    }
}

public class PyIntObjectType : PyTypeObject<PyIntObjectType, PyIntObject>
{
    public override string Name => "int";

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionArgsDef("number=0", "/")]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        if (arguments.Args[0] is PyStrObject str)
        {
            if (!TryParse(str.Value, 10, out var integer))
                return PyResult.RaiseValueError($"invalid literal for int() with base 10: '{str.Value}'");

            return PyIntObject.FromInteger(integer);
        }

        // TODO: __int__? __index__?
        if (!PySpecialMethods.TryGetIndex(context, arguments[0], out var value, out var result))
            return result;

        return PyIntObject.FromInteger(value.Value);
    }
    [PyFunctionArgsDef("string", "/", "base=10")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        if (arguments.Args[1] is not PyIntObject)
            return PyResult.RaiseTypeError($"'{arguments.Args[1].PyType.Name}' object cannot be interpreted as an integer");

        var numBase = (PyIntObject)arguments.Args[1];

        if (!((numBase.Value >= 2 && numBase.Value <= 36) || numBase.Value.IsZero))
            return PyResult.RaiseValueError("int() base must be >= 2 and <= 36, or 0");

        if (arguments.Args[0] is PyStrObject str)
        {
            if (!TryParse(str.Value, numBase.Int32Value, out var result))
                return PyResult.RaiseValueError($"invalid literal for int() with base {numBase.Value}: '{str.Value}'");

            return PyIntObject.FromInteger(result);
        }

        return PyResult.RaiseTypeError("int() can't convert non-string with explicit base");

    }

    internal static bool TryParse(ReadOnlySpan<char> s, int numBase, out BigInteger result)
    {
        result = default;

        s = s.Trim();
        if (s.IsEmpty)
            return false;

        bool negative = false;
        if (s[0] is '+' or '-')
        {
            negative = s[0] is '-';
            s = s[1..];
        }
        if (s.IsEmpty)
            return false;

        if (!char.IsAsciiDigit(s[0]))
            return false;

        if (numBase is 0)
        {
            if (s.StartsWith("0x") || s.StartsWith("0X"))
                numBase = 16;
            else if (s.StartsWith("0b") || s.StartsWith("0B"))
                numBase = 2;
            else if (s.StartsWith("0o") || s.StartsWith("0O"))
                numBase = 8;
            else
                numBase = 10;

            if (numBase is not 10)
            {
                s = s[2..];
                if (!ValidateAfterRemovingPrefix(s))
                    return false;
            }
        }
        else if (numBase is 16)
        {
            if (s.StartsWith("0x") || s.StartsWith("0X"))
            {
                s = s[2..];
                if (!ValidateAfterRemovingPrefix(s))
                    return false;
            }
        }
        else if (numBase is 2)
        {
            if (s.StartsWith("0b") || s.StartsWith("0B"))
            {
                s = s[2..];
                if (!ValidateAfterRemovingPrefix(s))
                    return false;
            }
        }
        else if (numBase is 8)
        {
            if (s.StartsWith("0o") || s.StartsWith("0O"))
            {
                s = s[2..];
                if (!ValidateAfterRemovingPrefix(s))
                    return false;
            }
        }

        bool containsUnderline = s.Contains('_');
        if (containsUnderline)
        {
            if (s[^1] is '_')
                return false;

            if (s.Contains("__", StringComparison.Ordinal))
                return false;
        }

        if (numBase is 10 && !containsUnderline)
        {
            if (!TryParseBase10(s, out result))
                return false;
        }
        else
        {
            if (!TryParseBaseN(s, numBase, out result))
                return false;
        }

        result = negative ? -result : result;
        return true;

        static bool ValidateAfterRemovingPrefix(ReadOnlySpan<char> s)
        {
            if (s.IsEmpty)
                return false;

            if (s[0] is '_')
            {
                s = s[1..];
                if (s.IsEmpty)
                    return false;

                if (s[0] is '_')
                    return false;
            }

            return true;
        }
    }

    private static bool TryParseBase10(ReadOnlySpan<char> s, out BigInteger result)
    {
        return BigInteger.TryParse(s, NumberStyles.None, provider: null, out result);
    }

    private static bool TryParseBaseN(ReadOnlySpan<char> s, int numBase, out BigInteger result)
    {
        result = 0;
        foreach (var c in s)
        {
            if (c is '_')
                continue;

            if (!TryConvertHexCharToInt(c, out var value))
                return false;

            if (value >= numBase)
                return false;

            result *= numBase;
            result += value;
        }

        return true;
    }

    private static bool TryConvertHexCharToInt(char c, out int value)
    {
        if (c >= CharToHexLookup.Length)
        {
            value = 0;
            return false;
        }

        value = CharToHexLookup[c];
        return value is not 0xFF;
    }

    public static ReadOnlySpan<byte> CharToHexLookup =>
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 15
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 31
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 47
        0x0,  0x1,  0x2,  0x3,  0x4,  0x5,  0x6,  0x7,  0x8,  0x9,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 63
        0xFF, 0xA,  0xB,  0xC,  0xD,  0xE,  0xF,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 79
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 95
        0xFF, 0xa,  0xb,  0xc,  0xd,  0xe,  0xf,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 111
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 127
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 143
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 159
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 175
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 191
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 207
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 223
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 239
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF  // 255
    ];

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var result = _new.Call(context, args, kwargs);
        if (result.IsError)
            return result;

        var obj = result.Value;
        Debug.Assert(obj is PyIntObject);
        var value = ((PyIntObject)obj).Value;
        if (cls != this && value > -PyIntObject.NegativePoolSize && value < PyIntObject.PositivesPoolSize)
            return PyIntObject.FromIntegerNoCache(value);

        obj._pyType = cls;
        return obj;
    }

    protected internal override PyResult Index(PyCallContext context, PyIntObject self)
    {
        return self;
    }

    protected internal override PyResult Hash(PyCallContext context, PyIntObject self)
    {
        return self;
    }

    protected internal override PyResult Repr(PyCallContext context, PyIntObject self)
    {
        return PyStrObject.FromString(self.Value.ToString());
    }

    protected internal override PyResult Bool(PyCallContext context, PyIntObject self)
    {
        return PyBoolObject.FromBoolean(self.Value != 0);
    }

    protected internal override PyResult Int(PyCallContext context, PyIntObject self)
    {
        return self;
    }

    protected internal override PyResult Float(PyCallContext context, PyIntObject self)
    {
        return PyFloatObject.FromDouble((double)self.Value);
    }

    protected internal override PyResult Neg(PyCallContext context, PyIntObject self)
    {
        return PyIntObject.FromInteger(-self.Value);
    }

    protected internal override PyResult Pos(PyCallContext context, PyIntObject self)
    {
        return self;
    }

    protected internal override PyResult Abs(PyCallContext context, PyIntObject self)
    {
        return self.Value >= 0 ? self : PyIntObject.FromInteger(-self.Value);
    }

    protected internal override PyResult Invert(PyCallContext context, PyIntObject self)
    {
        return PyIntObject.FromInteger(~self.Value);
    }

    protected internal override PyResult Add(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Add(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Add, self, intObj);
    }
    protected internal override PyResult Sub(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Sub(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Sub, self, intObj);
    }
    protected internal override PyResult Mul(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Mul(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Mul, self, intObj);
    }
    protected internal override PyResult TrueDiv(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.TrueDiv(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.TrueDiv, self, intObj);
    }
    protected internal override PyResult FloorDiv(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.FloorDiv(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.FloorDiv, self, intObj);
    }
    protected internal override PyResult Mod(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Mod(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Mod, self, intObj);
    }
    protected internal override PyResult DivMod(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.DivMod(context, self, other);
        var (q, r) = BigInteger.DivRem(self.Value, intObj.Value);
        return PyTupleObject.CreateTuple(PyIntObject.FromInteger(q), PyIntObject.FromInteger(r));
    }
    protected internal override PyResult Pow(PyCallContext context, PyIntObject self, PyObject other, PyObject modulo)
    {
        if (other is not PyIntObject intObj)
            return base.Pow(context, self, other, modulo);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Pow, self, intObj, modulo);
    }
    protected internal override PyResult LShift(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.LShift(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.LShift, self, intObj);
    }
    protected internal override PyResult RShift(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.RShift(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.RShift, self, intObj);
    }
    protected internal override PyResult And(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.And(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.And, self, intObj);
    }
    protected internal override PyResult Xor(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Xor(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Xor, self, intObj);
    }
    protected internal override PyResult Or(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Or(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Or, self, intObj);
    }
    protected internal override PyResult Lt(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Lt(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Lt, self, intObj);
    }
    protected internal override PyResult Gt(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Gt(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Gt, self, intObj);
    }
    protected internal override PyResult Le(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Le(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Le, self, intObj);
    }
    protected internal override PyResult Ge(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Ge(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Ge, self, intObj);
    }
    protected internal override PyResult Eq(PyCallContext context, PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Eq(context, self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Eq, self, intObj);
    }
}
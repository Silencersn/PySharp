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

    public override PyTypeObject DefaultPyType => PyIntObjectType2.Shared;

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

    protected internal override PyObject? IndexImpl()
    {
        return this;
    }

    protected internal override PyObject? HashImpl()
    {
        return this;
    }

    protected internal override PyObject? ReprImpl()
    {
        return PyStrObject.FromString(Value.ToString());
    }

    protected internal override PyObject? BoolImpl()
    {
        return PyBoolObject.FromBoolean(Value != 0);
    }

    protected internal override PyObject? IntImpl()
    {
        return this;
    }

    protected internal override PyObject? FloatImpl()
    {
        return PyFloatObject.FromDouble((double)Value);
    }

    protected internal override PyObject? NegImpl()
    {
        return FromInteger(-Value);
    }

    protected internal override PyObject? PosImpl()
    {
        return this;
    }

    protected internal override PyObject? AbsImpl()
    {
        return Value >= 0 ? this : NegImpl();
    }

    protected internal override PyObject? InvertImpl()
    {
        return FromInteger(~Value);
    }

    protected internal override PyObject? AddImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.AddImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Add, this, intObj);
    }
    protected internal override PyObject? SubImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.SubImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Sub, this, intObj);
    }
    protected internal override PyObject? MulImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.MulImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Mul, this, intObj);
    }
    protected internal override PyObject? TrueDivImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.TrueDivImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.TrueDiv, this, intObj);
    }
    protected internal override PyObject? FloorDivImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.FloorDivImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.FloorDiv, this, intObj);
    }
    protected internal override PyObject? ModImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.ModImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Mod, this, intObj);
    }
    protected internal override PyObject? DivModImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.DivModImpl(other);

        var (q, r) = BigInteger.DivRem(Value, intObj.Value);
        return PyTupleObject.CreateTuple(FromInteger(q), FromInteger(r));
    }
    protected internal override PyObject? PowImpl(PyObject other, PyObject modulo)
    {
        if (other is not PyIntObject intObj)
            return base.PowImpl(other, modulo);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Pow, this, intObj, modulo);
    }
    protected internal override PyObject? LShiftImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.LShiftImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.LShift, this, intObj);
    }
    protected internal override PyObject? RShiftImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.RShiftImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.RShift, this, intObj);
    }
    protected internal override PyObject? AndImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.AndImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.And, this, intObj);
    }
    protected internal override PyObject? XorImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.XorImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Xor, this, intObj);
    }
    protected internal override PyObject? OrImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.OrImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Or, this, intObj);
    }

    protected internal override PyObject? LtImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.LtImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Lt, this, intObj);
    }
    protected internal override PyObject? GtImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.GtImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Gt, this, intObj);
    }
    protected internal override PyObject? LeImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.LeImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Le, this, intObj);
    }
    protected internal override PyObject? GeImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.GeImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Ge, this, intObj);
    }
    protected internal override PyObject? EqImpl(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.EqImpl(other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Eq, this, intObj);
    }
}

public sealed class PyIntObjectType : PyPrimitiveTypeObject<PyIntObjectType, PyIntObject>
{
    public override string Name => "int";

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionArgsDef("number=0", "/")]
    private static PyObject? NewImpl_1(PyArguments arguments)
    {
        if (arguments.Args[0] is PyStrObject str)
        {
            if (!TryParse(str.Value, 10, out var result))
                return PyVirtualMachine.RaiseValueError($"invalid literal for int() with base 10: '{str.Value}'");

            return PyIntObject.FromInteger(result);
        }

        return PySpecialMethods.GetInt(arguments.Args[0]);
    }
    [PyFunctionArgsDef("string", "/", "base=10")]
    private static PyObject? NewImpl_2(PyArguments arguments)
    {
        if (arguments.Args[1] is not PyIntObject)
            return PyVirtualMachine.RaiseTypeError($"'{arguments.Args[1].PyType.Name}' object cannot be interpreted as an integer");

        var numBase = (PyIntObject)arguments.Args[1];

        if (!((numBase.Value >= 2 && numBase.Value <= 36) || numBase.Value.IsZero))
            return PyVirtualMachine.RaiseValueError("int() base must be >= 2 and <= 36, or 0");

        if (arguments.Args[0] is PyStrObject str)
        {
            if (!TryParse(str.Value, numBase.Int32Value, out var result))
                return PyVirtualMachine.RaiseValueError($"invalid literal for int() with base {numBase.Value}: '{str.Value}'");

            return PyIntObject.FromInteger(result);
        }

        return PyVirtualMachine.RaiseTypeError("int() can't convert non-string with explicit base");

    }

    protected internal override PyObject? NewImpl(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(args, kwargs);
        if (obj is null)
            return null;

        Debug.Assert(obj is PyIntObject);
        var value = ((PyIntObject)obj).Value;
        if (cls != this && value > -PyIntObject.NegativePoolSize && value < PyIntObject.PositivesPoolSize)
            return PyIntObject.FromIntegerNoCache(value);
        return obj;
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
}

public class PyIntObjectType2 : PyTypeObject<PyIntObject>
{
    public static PyTypeObject Shared { get; } = new PyIntObjectType2();
    public override string Name => "int";

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionArgsDef("number=0", "/")]
    private static PyObject? NewImpl_1(PyArguments arguments)
    {
        if (arguments.Args[0] is PyStrObject str)
        {
            if (!TryParse(str.Value, 10, out var result))
                return PyVirtualMachine.RaiseValueError($"invalid literal for int() with base 10: '{str.Value}'");

            return PyIntObject.FromInteger(result);
        }

        return PySpecialMethods.GetInt(arguments.Args[0]);
    }
    [PyFunctionArgsDef("string", "/", "base=10")]
    private static PyObject? NewImpl_2(PyArguments arguments)
    {
        if (arguments.Args[1] is not PyIntObject)
            return PyVirtualMachine.RaiseTypeError($"'{arguments.Args[1].PyType.Name}' object cannot be interpreted as an integer");

        var numBase = (PyIntObject)arguments.Args[1];

        if (!((numBase.Value >= 2 && numBase.Value <= 36) || numBase.Value.IsZero))
            return PyVirtualMachine.RaiseValueError("int() base must be >= 2 and <= 36, or 0");

        if (arguments.Args[0] is PyStrObject str)
        {
            if (!TryParse(str.Value, numBase.Int32Value, out var result))
                return PyVirtualMachine.RaiseValueError($"invalid literal for int() with base {numBase.Value}: '{str.Value}'");

            return PyIntObject.FromInteger(result);
        }

        return PyVirtualMachine.RaiseTypeError("int() can't convert non-string with explicit base");

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


    protected internal override PyResult New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(args, kwargs);
        if (obj is null)
            return PyResult.CaptureExceptionFromPVM();

        Debug.Assert(obj is PyIntObject);
        var value = ((PyIntObject)obj).Value;
        if (cls != this && value > -PyIntObject.NegativePoolSize && value < PyIntObject.PositivesPoolSize)
            return PyIntObject.FromIntegerNoCache(value);
        return obj;
    }

    protected internal override PyResult Index(PyIntObject self)
    {
        return self;
    }

    protected internal override PyResult Hash(PyIntObject self)
    {
        return self;
    }

    protected internal override PyResult Repr(PyIntObject self)
    {
        return PyStrObject.FromString(self.Value.ToString());
    }

    protected internal override PyResult Bool(PyIntObject self)
    {
        return PyBoolObject.FromBoolean(self.Value != 0);
    }

    protected internal override PyResult Int(PyIntObject self)
    {
        return self;
    }

    protected internal override PyResult Float(PyIntObject self)
    {
        return PyFloatObject.FromDouble((double)self.Value);
    }

    protected internal override PyResult Neg(PyIntObject self)
    {
        return PyIntObject.FromInteger(-self.Value);
    }

    protected internal override PyResult Pos(PyIntObject self)
    {
        return self;
    }

    protected internal override PyResult Abs(PyIntObject self)
    {
        return self.Value >= 0 ? self : PyIntObject.FromInteger(-self.Value);
    }

    protected internal override PyResult Invert(PyIntObject self)
    {
        return PyIntObject.FromInteger(~self.Value);
    }

    protected internal override PyResult Add(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Add(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Add, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult Sub(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Sub(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Sub, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult Mul(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Mul(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Mul, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult TrueDiv(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.TrueDiv(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.TrueDiv, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult FloorDiv(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.FloorDiv(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.FloorDiv, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult Mod(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Mod(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Mod, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult DivMod(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.DivMod(self, other);
        var (q, r) = BigInteger.DivRem(self.Value, intObj.Value);
        return PyTupleObject.CreateTuple(PyIntObject.FromInteger(q), PyIntObject.FromInteger(r));
    }
    protected internal override PyResult Pow(PyIntObject self, PyObject other, PyObject modulo)
    {
        if (other is not PyIntObject intObj)
            return base.Pow(self, other, modulo);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Pow, self, intObj, modulo) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult LShift(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.LShift(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.LShift, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult RShift(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.RShift(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.RShift, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult And(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.And(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.And, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult Xor(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Xor(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Xor, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult Or(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Or(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Or, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult Lt(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Lt(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Lt, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult Gt(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Gt(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Gt, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult Le(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Le(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Le, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult Ge(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Ge(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Ge, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
    protected internal override PyResult Eq(PyIntObject self, PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Eq(self, other);
        return PyMath.CalculatePyIntObject(PyOperatorTypes.Eq, self, intObj) ?? PyResult.CaptureExceptionFromPVM();
    }
}
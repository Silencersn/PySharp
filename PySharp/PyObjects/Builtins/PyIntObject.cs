using PySharp.PyRuntime;
using PySharp.PyRuntime.PyAttributes;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace PySharp.PyObjects.Builtins;

public class PyIntObject : PyObject
{
    private static readonly PyIntObject[] _negatives;
    private static readonly PyIntObject[] _positives;

    static PyIntObject()
    {
        _negatives = new PyIntObject[6];
        _positives = new PyIntObject[257];

        for (int i = 0; i < _negatives.Length; i++)
        {
            _negatives[i] = new PyIntObject(-i);
        }
        for (int i = 0; i < _positives.Length; i++)
        {
            _positives[i] = new PyIntObject(i);
        }
    }

    public override PyTypeObject PyType => PyBuiltinTypes.Int;

    public BigInteger Value { get; }
    public int Int32Value => (int)Value;

    public PyIntObject()
    {
    }

    public PyIntObject(BigInteger value) : this()
    {
        Value = value;
    }


    public static PyIntObject FromInteger(int value)
    {
        if (value <= 256)
        {
            if (value >= 0)
                return _positives[value];

            if (value >= -5)
                return _negatives[-value];
        }

        return new PyIntObject(value);
    }
    public static PyIntObject FromInteger(BigInteger value)
    {
        if (value <= 256)
        {
            if (value >= 0)
                return _positives[(int)value];

            if (value >= -5)
                return _negatives[-(int)value];
        }

        return new PyIntObject(value);
    }

    public override PyIntObject Index()
    {
        return this;
    }

    public override PyIntObject Hash()
    {
        return this;
    }

    public override PyStrObject Repr()
    {
        return Value.ToString();
    }

    public override PyBoolObject Bool()
    {
        return PyBoolObject.FromBoolean(Value != 0);
    }

    public override PyIntObject Int()
    {
        return this;
    }

    public override PyObject? Float()
    {
        return PyFloatObject.FromDouble((double)Value);
    }

    public override PyObject? Neg()
    {
        return FromInteger(-Value);
    }

    public override PyObject? Pos()
    {
        return this;
    }

    public override PyObject? Abs()
    {
        return Value >= 0 ? this : Neg();
    }

    public override PyObject? Invert()
    {
        return new PyIntObject(-(Value + 1));
    }

    public override PyObject? Add(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Add(other);
        return FromInteger(Value + intObj.Value);
    }
    public override PyObject? Sub(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Sub(other);

        return FromInteger(Value - intObj.Value);
    }
    public override PyObject? Mul(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Mul(other);

        return FromInteger(Value * intObj.Value);
    }
    public override PyObject? TrueDiv(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.TrueDiv(other);

        if (intObj.Value == 0)
            return PyVirtualMachine.RaiseZeroDivisionError("division by zero");

        return PyFloatObject.FromDouble((double)Value / (double)intObj.Value);
    }
    public override PyObject? FloorDiv(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.FloorDiv(other);

        if (intObj.Value == 0)
            return PyVirtualMachine.RaiseZeroDivisionError("division by zero");

        var (q, r) = BigInteger.DivRem(Value, intObj.Value);
        if (r.IsZero || BigInteger.IsPositive(q))
            return FromInteger(q);
        return FromInteger(q - 1);
    }
    public override PyObject? Mod(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Mod(other);

        return FromInteger(Value % intObj.Value);
    }
    public override PyObject? DivMod(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.DivMod(other);

        var (q, r) = BigInteger.DivRem(Value, intObj.Value);
        return PyTupleObject.CreateTuple(FromInteger(q), FromInteger(r));
    }
    public override PyObject? Pow(PyObject other, PyObject modulo)
    {
        if (other is not PyIntObject intObj)
            return base.Pow(other, modulo);

        if (modulo is PyNoneObject)
        {
            if (intObj.Value >= 0)
                return FromInteger(BigInteger.Pow(Value, intObj.Int32Value));

            return PyFloatObject.FromDouble(double.Pow((double)Value, intObj.Int32Value));
        }
        else
        {
            if (modulo is not PyIntObject moduloObj)
                return base.Pow(other, modulo);

            if (intObj.Value >= 0)
                return FromInteger(BigInteger.Pow(Value, intObj.Int32Value) % moduloObj.Value);

            return PyFloatObject.FromDouble(double.Pow((double)Value, intObj.Int32Value) % moduloObj.Int32Value);
        }
    }
    public override PyObject? LShift(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.LShift(other);

        return FromInteger(Value << intObj.Int32Value);
    }
    public override PyObject? RShift(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.RShift(other);

        return FromInteger(Value >> intObj.Int32Value);
    }
    public override PyObject? And(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.And(other);

        return FromInteger(Value & intObj.Value);
    }
    public override PyObject? Xor(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Xor(other);

        return FromInteger(Value ^ intObj.Value);
    }
    public override PyObject? Or(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Or(other);

        return FromInteger(Value | intObj.Value);
    }

    public override PyObject? Lt(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Add(other);

        return PyBoolObject.FromBoolean(Value < intObj.Value);
    }
    public override PyObject? Gt(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Add(other);

        return PyBoolObject.FromBoolean(Value > intObj.Value);
    }
    public override PyObject? Le(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Add(other);

        return PyBoolObject.FromBoolean(Value <= intObj.Value);
    }
    public override PyObject? Ge(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Add(other);

        return PyBoolObject.FromBoolean(Value >= intObj.Value);
    }
    public override PyObject? Eq(PyObject other)
    {
        if (other is not PyIntObject intObj)
            return base.Add(other);

        return PyBoolObject.FromBoolean(Value == intObj.Value);
    }
}

public sealed class PyIntObjectType : PyTypeObject
{
    public PyIntObjectType()
    {
        AppendSpecialMethodsAsDescriptors<PyIntObject>();
    }

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

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(args, kwargs);
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

        if (numBase is 10)
        {
            if (containsUnderline)
            {
                if (!TryParseBaseNLessThanOrEqual10(s, numBase, out result))
                    return false;
            }
            else
            {
                if (!TryParseBase10(s, out result))
                    return false;
            }
        }
        else if (numBase < 10)
        {
            if (!TryParseBaseNLessThanOrEqual10(s, numBase, out result))
                return false;
        }
        else
        {
            if (!TryParseBaseNGreaterThan10(s, numBase, out result))
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

    private static bool TryParseBaseNLessThanOrEqual10(ReadOnlySpan<char> s, int numBase, out BigInteger result)
    {
        Debug.Assert(numBase >= 2 && numBase <= 10);

        result = 0;
        foreach (var c in s)
        {
            if (c is '_')
                continue;

            if (!TryConvertCharToIntLessThan10(c, out var value))
                return false;

            if (value >= numBase)
                return false;

            result *= numBase;
            result += value;
        }

        return true;
    }

    private static bool TryParseBaseNGreaterThan10(ReadOnlySpan<char> s, int numBase, out BigInteger result)
    {
        Debug.Assert(numBase > 10 && numBase <= 36);

        result = 0;
        foreach (var c in s)
        {
            if (c is '_')
                continue;

            if (!TryConvertCharToInt(c, out var value))
                return false;

            if (value >= numBase)
                return false;

            result *= numBase;
            result += value;
        }

        return true;
    }

    private static bool TryConvertCharToIntLessThan10(char c, out int value)
    {
        if (char.IsAsciiDigit(c))
        {
            value = c - '0';
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryConvertCharToInt(char c, out int value)
    {
        if (char.IsAsciiDigit(c))
        {
            value = c - '0';
            return true;
        }
        else if (char.IsAsciiLetterLower(c))
        {
            value = c - 'a' + 10;
            return true;
        }
        else if (char.IsAsciiLetterUpper(c))
        {
            value = c - 'A' + 10;
            return true;
        }

        value = 0;
        return false;
    }
}
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

using PySharp.Utility;

namespace PySharp.Modules.Builtins;

public class PyFloatObject : PyObject
{
    public static PyFloatObject Zero { get; } = FromDouble(0);
    public static PyFloatObject NegativeZero { get; } = FromDouble(double.NegativeZero);
    public static PyFloatObject One { get; } = FromDouble(1);
    public static PyFloatObject MinusOne { get; } = FromDouble(-1);
    public static PyFloatObject NaN { get; } = FromDouble(double.NaN);
    public static PyFloatObject PositiveInfinity { get; } = FromDouble(double.PositiveInfinity);
    public static PyFloatObject NegativeInfinity { get; } = FromDouble(double.NegativeInfinity);
    public static PyFloatObject Pi { get; } = FromDouble(double.Pi);
    public static PyFloatObject E { get; } = FromDouble(double.E);
    public static PyFloatObject Epsilon { get; } = FromDouble(double.Epsilon);
    public static PyFloatObject Tau { get; } = FromDouble(double.Tau);


    public double Value { get; }
    public override PyTypeObject DefaultPyType => PyFloatObjectType.Shared;

    private PyFloatObject(double value)
    {
        Value = value;
    }
    public static PyFloatObject FromDouble(double value)
    {
        return new PyFloatObject(value);
    }

    // Exact comparison of a double with a BigInteger (CPython-style precise
    // comparison, no (double)BigInteger precision loss / silent inf).
    // Returns <0 if f < i, 0 if f == i, >0 if f > i; null when f is NaN
    // (unordered: all comparisons are false, matching CPython).
    internal static int? CompareDoubleWithInt(double f, BigInteger i)
    {
        if (double.IsNaN(f))
            return null;
        if (double.IsPositiveInfinity(f))
            return 1;
        if (double.IsNegativeInfinity(f))
            return -1;
        if (f is 0)
            return -i.Sign;

        long bits = BitConverter.DoubleToInt64Bits(f);
        bool neg = bits < 0;
        int expField = (int)((bits >> 52) & 0x7FF);
        long mant = bits & 0xFFFFFFFFFFFFFL;

        BigInteger m;
        int e;
        if (expField is 0)
        {
            m = mant;               // subnormal: mant * 2^-1074
            e = -1074;
        }
        else
        {
            m = (BigInteger.One << 52) | mant;   // normal: (2^52 + mant) * 2^(expField-1023-52)
            e = expField - 1023 - 52;
        }
        if (neg)
            m = -m;

        // now f == m * 2^e exactly
        if (i.Sign != m.Sign)
            return m.Sign.CompareTo(i.Sign);

        BigInteger a, b;
        if (e >= 0)
        {
            a = m << e;   // f as integer
            b = i;
        }
        else
        {
            a = m;         // f = m * 2^e, compare m vs i * 2^(-e)
            b = i << -e;
        }
        return a.CompareTo(b);
    }
}

[PyType("float")]
public sealed partial class PyFloatObjectType : PyTypeObject<PyFloatObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl_1))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("number=0.0", "/")]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is PyStrObject str)
        {
            if (!TryParseFloatString(str.Value, out var value))
                return PyResult.ValueError(PySR.Runtime_Number_Float_InvalidLiteral, str.Value);

            return PyFloatObject.FromDouble(value);
        }

        return PySpecialMethods.Float(context, arguments[0]);
    }

    protected override PyResult Repr(PyCallContext context, PyFloatObject self)
    {
        var val = self.Value;
        if (double.IsNaN(val))
            return PyStrObject.FromString("nan");
        if (double.IsInfinity(val))
            return PyStrObject.FromString(val > 0 ? "inf" : "-inf");

        return PyStrObject.FromString(FormatShortestRepr(val));
    }

    // CPython repr renders the shortest round-trip digit string (dtoa mode 0)
    // with format_float_short presentation: scientific notation iff the
    // decimal point sits at position <= -4 or > 16, exponent at least two
    // digits, integral values keep a trailing ".0", always lowercase 'e'.
    // .NET "G" yields the identical shortest digits, so only the
    // presentation is rebuilt here.
    internal static string FormatShortestRepr(double val)
    {
        if (val is 0.0)
            return double.IsNegative(val) ? "-0.0" : "0.0";

        var text = val.ToString("G", CultureInfo.InvariantCulture);
        var negative = text[0] is '-';
        if (negative)
            text = text[1..];

        string digits;
        int decpt;
        var eIndex = text.IndexOfAny(['e', 'E']);
        if (eIndex >= 0)
        {
            digits = text[..eIndex].Replace(".", string.Empty);
            decpt = int.Parse(text[(eIndex + 1)..], CultureInfo.InvariantCulture) + 1;
        }
        else
        {
            var point = text.IndexOf('.');
            if (point >= 0)
            {
                digits = text.Remove(point, 1);
                decpt = point;
            }
            else
            {
                digits = text;
                decpt = text.Length;
            }
        }

        // a fixed-form "0.0001" carries leading zeros with no significance
        var firstSignificant = 0;
        while (firstSignificant < digits.Length - 1 && digits[firstSignificant] is '0')
            firstSignificant++;
        if (firstSignificant > 0)
        {
            digits = digits[firstSignificant..];
            decpt -= firstSignificant;
        }

        var sb = new System.Text.StringBuilder();
        if (negative)
            sb.Append('-');

        if (decpt <= -4 || decpt > 16)
        {
            var last = digits.Length - 1;
            while (last > 0 && digits[last] is '0')
                last--;

            sb.Append(digits[0]);
            if (last > 0)
                sb.Append('.').Append(digits, 1, last);

            var exp = decpt - 1;
            sb.Append('e')
                .Append(exp < 0 ? '-' : '+')
                .AppendFormat(CultureInfo.InvariantCulture, "{0:D2}", Math.Abs(exp));
        }
        else if (decpt <= 0)
        {
            sb.Append("0.").Append('0', -decpt).Append(digits);
        }
        else if (decpt >= digits.Length)
        {
            sb.Append(digits).Append('0', decpt - digits.Length).Append(".0");
        }
        else
        {
            sb.Append(digits, 0, decpt).Append('.').Append(digits, decpt, digits.Length - decpt);
        }

        return sb.ToString();
    }

    protected override PyResult Hash(PyCallContext context, PyFloatObject self)
    {
        // CPython _Py_HashDouble: hash(1.0) == hash(1) == 1, hash(-1.0) == -2,
        // inf -> +/-314159, NaN -> object identity hash.
        return PyIntObject.FromInteger(PyHash.HashDouble(self.Value, self));
    }

    protected override PyResult Bool(PyCallContext context, PyFloatObject self)
    {
        return PyBoolObject.FromBoolean(self.Value is not 0);
    }

    protected override PyResult Int(PyCallContext context, PyFloatObject self)
    {
        // CPython float_to_long: NaN -> ValueError, infinity -> OverflowError;
        // .NET's BigInteger(double) ctor would hard-crash on both.
        if (double.IsNaN(self.Value))
            return PyResult.ValueError(PySR.Runtime_Float_CannotConvertNaNToInteger);
        if (double.IsInfinity(self.Value))
            return PyResult.OverflowError(PySR.Runtime_Float_CannotConvertInfinityToInteger);
        return new PyIntObject((BigInteger)self.Value);
    }

    protected override PyResult Float(PyCallContext context, PyFloatObject self)
    {
        return self;
    }

    protected override PyResult Neg(PyCallContext context, PyFloatObject self)
    {
        return PyFloatObject.FromDouble(-self.Value);
    }

    protected override PyResult Pos(PyCallContext context, PyFloatObject self)
    {
        return self;
    }

    protected override PyResult Abs(PyCallContext context, PyFloatObject self)
    {
        // float_abs clears the sign bit: abs(-0.0) is +0.0, never a
        // negation round-trip that would keep the zero's sign
        return PyFloatObject.FromDouble(double.Abs(self.Value));
    }

    protected override PyResult Add(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => intObj.Value.TryToDoubleRounded(out var d) ? PyFloatObject.FromDouble(self.Value + d) : PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat),
            PyFloatObject floatObj => PyFloatObject.FromDouble(self.Value + floatObj.Value),
            _ => base.Add(context, self, other),
        };
    }
    protected override PyResult Sub(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => intObj.Value.TryToDoubleRounded(out var d) ? PyFloatObject.FromDouble(self.Value - d) : PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat),
            PyFloatObject floatObj => PyFloatObject.FromDouble(self.Value - floatObj.Value),
            _ => base.Sub(context, self, other),
        };
    }
    protected override PyResult Mul(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => intObj.Value.TryToDoubleRounded(out var d) ? PyFloatObject.FromDouble(self.Value * d) : PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat),
            PyFloatObject floatObj => PyFloatObject.FromDouble(self.Value * floatObj.Value),
            _ => base.Mul(context, self, other),
        };
    }
    protected override PyResult TrueDiv(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => intObj.Value.IsZero ? PyResult.ZeroDivisionError() : intObj.Value.TryToDoubleRounded(out var d) ? PyFloatObject.FromDouble(self.Value / d) : PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat),
            PyFloatObject floatObj => floatObj.Value is 0 ? PyResult.ZeroDivisionError() : PyFloatObject.FromDouble(self.Value / floatObj.Value),
            _ => base.TrueDiv(context, self, other),
        };
    }
    protected override PyResult FloorDiv(PyCallContext context, PyFloatObject self, PyObject other)
    {
        if (other is PyIntObject intObj)
        {
            if (intObj.Value.IsZero)
                return PyResult.ZeroDivisionError();
            if (!intObj.Value.TryToDoubleRounded(out var dv))
                return PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
            FloatDivMod(self.Value, dv, out var floorDiv, out _);
            return PyFloatObject.FromDouble(floorDiv);
        }
        if (other is PyFloatObject floatObj)
        {
            if (floatObj.Value is 0)
                return PyResult.ZeroDivisionError();
            FloatDivMod(self.Value, floatObj.Value, out var floorDiv, out _);
            return PyFloatObject.FromDouble(floorDiv);
        }
        return base.FloorDiv(context, self, other);
    }
    protected override PyResult DivMod(PyCallContext context, PyFloatObject self, PyObject other)
    {
        // CPython derives both halves from one _float_div_mod pass;
        // composing separate floor-div and mod calls can disagree
        switch (other)
        {
            case PyIntObject intObj:
                if (intObj.Value.IsZero)
                    return PyResult.ZeroDivisionError();
                if (!intObj.Value.TryToDoubleRounded(out var dv))
                    return PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
                FloatDivMod(self.Value, dv, out var q, out var m);
                return PyTupleObject.CreateTuple(PyFloatObject.FromDouble(q), PyFloatObject.FromDouble(m));
            case PyFloatObject floatObj:
                if (floatObj.Value is 0)
                    return PyResult.ZeroDivisionError();
                FloatDivMod(self.Value, floatObj.Value, out var qf, out var mf);
                return PyTupleObject.CreateTuple(PyFloatObject.FromDouble(qf), PyFloatObject.FromDouble(mf));
            default:
                return base.DivMod(context, self, other);
        }
    }
    protected override PyResult Mod(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => intObj.Value.IsZero ? PyResult.ZeroDivisionError() : intObj.Value.TryToDoubleRounded(out var d) ? PyFloatObject.FromDouble(FloatMod(self.Value, d)) : PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat),
            PyFloatObject floatObj => floatObj.Value is 0 ? PyResult.ZeroDivisionError() : PyFloatObject.FromDouble(FloatMod(self.Value, floatObj.Value)),
            _ => base.Mod(context, self, other),
        };
    }

    // Python modulo semantics for floats: the result has the sign of the
    // divisor. C# '%' is a remainder (sign follows the dividend); CPython's
    // float_rem (Objects/floatobject.c) fixes that with fmod + a correction,
    // and a zero remainder keeps the divisor's sign (e.g. 4.0 % -2.0 == -0.0).
    private static double FloatMod(double left, double right)
    {
        var mod = left % right;
        if (mod is not 0)
        {
            if ((right < 0) != (mod < 0))
                mod += right;
        }
        else
        {
            mod = Math.CopySign(0.0, right);
        }
        return mod;
    }

    // CPython _float_div_mod (floatobject.c): the quotient derives from the
    // fmod remainder rather than Floor(a/b), so infinite operands yield nan
    // (inf // 2) and a zero quotient takes the sign of the true quotient
    // (-2 // inf == -1.0, not -0.0)
    private static void FloatDivMod(double vx, double wx, out double floorDiv, out double mod)
    {
        mod = vx % wx;
        var div = (vx - mod) / wx;
        if (mod is not 0)
        {
            // a nan remainder (infinite input) compares false and skips the fix
            if ((wx < 0) != (mod < 0))
            {
                mod += wx;
                div -= 1.0;
            }
        }
        else
        {
            mod = Math.CopySign(0.0, wx);
        }
        if (div is not 0)
        {
            floorDiv = double.Floor(div);
            if (div - floorDiv > 0.5)
                floorDiv += 1.0;
        }
        else
        {
            floorDiv = Math.CopySign(0.0, vx / wx);
        }
    }
    protected override PyResult Pow(PyCallContext context, PyFloatObject self, PyObject other, PyObject modulo)
    {
        if (modulo is not PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Number_PowThirdArgNotInteger);
        return other switch
        {
            PyIntObject intObj => intObj.Value.TryToDoubleRounded(out var d) ? Pow(self.Value, d) : PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat),
            PyFloatObject floatObj => Pow(self.Value, floatObj.Value),
            _ => base.Pow(context, self, other, modulo),
        };
    }
    // CPython float_pow: 0.0 (including -0.0) to a negative power raises
    // ZeroDivisionError instead of returning inf (-0.0 < 0 is false, so
    // 0.0 ** -0.0 keeps returning 1.0).
    private static PyResult Pow(double baseValue, double exponent)
    {
        if (baseValue is 0.0 && exponent < 0)
            return PyResult.ZeroDivisionError(PySR.Runtime_Number_ZeroToNegativePower);
        // CPython float_pow: a finite negative base with a non-integral
        // exponent returns NotImplemented and complex_pow evaluates the
        // principal value instead; the infinite/NaN special cases above
        // the negative-base check keep their float results.
        if (baseValue < 0 && double.IsFinite(baseValue) && double.IsFinite(exponent) && exponent != double.Floor(exponent))
            return PyComplexObject.FromComplex(ComplexPowPrincipal(baseValue, exponent));
        var result = double.Pow(baseValue, exponent);
        // CPython float_pow runs the platform pow only with finite
        // operands (the special cases above handle the rest), so an
        // infinite result is an ERANGE overflow reported through
        // PyErr_SetFromErrno; underflow stays silent.
        if (double.IsInfinity(result) && double.IsFinite(baseValue) && double.IsFinite(exponent))
            return ErrnoOverflow();
        return PyFloatObject.FromDouble(result);
    }

    // CPython c_pow (complexobject.c) for a negative real base and a
    // real exponent: the base's argument is +pi, and the exponent's
    // imaginary part is zero so no log adjustment applies.
    private static System.Numerics.Complex ComplexPowPrincipal(double baseValue, double exponent)
    {
        double len = Math.Pow(double.Hypot(baseValue, 0), exponent);
        double phase = Math.Atan2(0, baseValue) * exponent;
        return new(len * Math.Cos(phase), len * Math.Sin(phase));
    }

    // PyErr_SetFromErrno(OverflowError): args (ERANGE, strerror(ERANGE)).
    private static PyResult ErrnoOverflow()
    {
        var exception = PyExceptionObject.UnsafeCreate(
            PyOverflowErrorObjectType.Shared,
            [PyIntObject.FromInteger(34), PyStrObject.FromString("Result too large")]);
        return PyResult.FromException(exception);
    }
    protected override PyResult RAdd(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return Add(context, self, other);
    }
    protected override PyResult RSub(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => intObj.Value.TryToDoubleRounded(out var d) ? PyFloatObject.FromDouble(d - self.Value) : PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat),
            PyFloatObject floatObj => PyFloatObject.FromDouble(floatObj.Value - self.Value),
            _ => base.RSub(context, self, other),
        };
    }
    protected override PyResult RMul(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return Mul(context, self, other);
    }
    protected override PyResult RTrueDiv(PyCallContext context, PyFloatObject self, PyObject other)
    {
        if (self.Value is 0)
            return PyResult.ZeroDivisionError();

        return other switch
        {
            PyIntObject intObj => intObj.Value.TryToDoubleRounded(out var d) ? PyFloatObject.FromDouble(d / self.Value) : PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat),
            PyFloatObject floatObj => PyFloatObject.FromDouble(floatObj.Value / self.Value),
            _ => base.RTrueDiv(context, self, other),
        };
    }
    protected override PyResult RFloorDiv(PyCallContext context, PyFloatObject self, PyObject other)
    {
        if (self.Value is 0)
            return PyResult.ZeroDivisionError();

        if (other is PyIntObject intObj)
        {
            if (!intObj.Value.TryToDoubleRounded(out var dv))
                return PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
            FloatDivMod(dv, self.Value, out var floorDiv, out _);
            return PyFloatObject.FromDouble(floorDiv);
        }
        if (other is PyFloatObject floatObj)
        {
            FloatDivMod(floatObj.Value, self.Value, out var floorDiv, out _);
            return PyFloatObject.FromDouble(floorDiv);
        }
        return base.RFloorDiv(context, self, other);
    }
    protected override PyResult RDivMod(PyCallContext context, PyFloatObject self, PyObject other)
    {
        if (self.Value is 0)
            return PyResult.ZeroDivisionError();

        switch (other)
        {
            case PyIntObject intObj:
                if (!intObj.Value.TryToDoubleRounded(out var dv))
                    return PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
                FloatDivMod(dv, self.Value, out var q, out var m);
                return PyTupleObject.CreateTuple(PyFloatObject.FromDouble(q), PyFloatObject.FromDouble(m));
            case PyFloatObject floatObj:
                FloatDivMod(floatObj.Value, self.Value, out var qf, out var mf);
                return PyTupleObject.CreateTuple(PyFloatObject.FromDouble(qf), PyFloatObject.FromDouble(mf));
            default:
                return base.RDivMod(context, self, other);
        }
    }
    protected override PyResult RMod(PyCallContext context, PyFloatObject self, PyObject other)
    {
        if (self.Value is 0)
            return PyResult.ZeroDivisionError();

        return other switch
        {
            PyIntObject intObj => intObj.Value.TryToDoubleRounded(out var d) ? PyFloatObject.FromDouble(FloatMod(d, self.Value)) : PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat),
            PyFloatObject floatObj => PyFloatObject.FromDouble(FloatMod(floatObj.Value, self.Value)),
            _ => base.RMod(context, self, other),
        };
    }
    protected override PyResult RPow(PyCallContext context, PyFloatObject self, PyObject other, PyObject modulo)
    {
        if (modulo is not PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Number_PowThirdArgNotInteger);
        return other switch
        {
            PyIntObject intObj => intObj.Value.TryToDoubleRounded(out var d) ? Pow(d, self.Value) : PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat),
            PyFloatObject floatObj => Pow(floatObj.Value, self.Value),
            _ => base.RPow(context, self, other, modulo),
        };
    }
    protected override PyResult Lt(PyCallContext context, PyFloatObject self, PyObject other)
    {
        if (other is PyIntObject intObj)
            return PyBoolObject.FromBoolean(PyFloatObject.CompareDoubleWithInt(self.Value, intObj.Value) < 0);
        if (other is PyFloatObject floatObj)
            return PyBoolObject.FromBoolean(self.Value < floatObj.Value);
        return base.Lt(context, self, other);
    }
    protected override PyResult Gt(PyCallContext context, PyFloatObject self, PyObject other)
    {
        if (other is PyIntObject intObj)
            return PyBoolObject.FromBoolean(PyFloatObject.CompareDoubleWithInt(self.Value, intObj.Value) > 0);
        if (other is PyFloatObject floatObj)
            return PyBoolObject.FromBoolean(self.Value > floatObj.Value);
        return base.Gt(context, self, other);
    }
    protected override PyResult Le(PyCallContext context, PyFloatObject self, PyObject other)
    {
        if (other is PyIntObject intObj)
            return PyBoolObject.FromBoolean(PyFloatObject.CompareDoubleWithInt(self.Value, intObj.Value) <= 0);
        if (other is PyFloatObject floatObj)
            return PyBoolObject.FromBoolean(self.Value <= floatObj.Value);
        return base.Le(context, self, other);
    }
    protected override PyResult Ge(PyCallContext context, PyFloatObject self, PyObject other)
    {
        if (other is PyIntObject intObj)
            return PyBoolObject.FromBoolean(PyFloatObject.CompareDoubleWithInt(self.Value, intObj.Value) >= 0);
        if (other is PyFloatObject floatObj)
            return PyBoolObject.FromBoolean(self.Value >= floatObj.Value);
        return base.Ge(context, self, other);
    }
    protected override PyResult Eq(PyCallContext context, PyFloatObject self, PyObject other)
    {
        if (other is PyIntObject intObj)
            return PyBoolObject.FromBoolean(PyFloatObject.CompareDoubleWithInt(self.Value, intObj.Value) is 0);
        if (other is PyFloatObject floatObj)
            return PyBoolObject.FromBoolean(self.Value == floatObj.Value);
        return base.Eq(context, self, other);
    }

    [PyMethod("conjugate")]
    [PyFunctionParameters]
    private static PyResult Conjugate(PyCallContext context, PyFloatObject self, PyArguments arguments)
    {
        return self;
    }

    [PyMethod("is_integer")]
    [PyFunctionParameters]
    private static PyResult IsInteger(PyCallContext context, PyFloatObject self, PyArguments arguments)
    {
        if (!double.IsFinite(self.Value))
            return PyBoolObject.False;
        return PyBoolObject.FromBoolean(self.Value == Math.Truncate(self.Value));
    }

    [PyProperty("real")]
    private static PyResult Get_Real(PyCallContext context, PyFloatObject self)
    {
        return self;
    }

    [PyProperty("imag")]
    private static PyResult Get_Imag(PyCallContext context, PyFloatObject self)
    {
        return PyFloatObject.Zero;
    }

    [PyMethod("as_integer_ratio")]
    [PyFunctionParameters]
    private static PyResult AsIntegerRatio(PyCallContext context, PyFloatObject self, PyArguments arguments)
    {
        var val = self.Value;
        // CPython deliberately distinguishes the two special values:
        // infinities raise OverflowError, NaN raises ValueError
        if (double.IsInfinity(val))
            return PyResult.OverflowError(PySR.Runtime_Float_CannotConvertInfinityToIntegerRatio);
        if (double.IsNaN(val))
            return PyResult.ValueError(PySR.Runtime_Float_CannotConvertNaNToIntegerRatio);

        long bits = BitConverter.DoubleToInt64Bits(val);
        bool negative = (bits >> 63) is not 0;
        int exp = (int)((bits >> 52) & 0x7FF) - 1023;
        long mantissa = bits & 0xFFFFFFFFFFFFFL;

        BigInteger numerator, denominator;

        if (exp is -1023) // subnormal
        {
            numerator = mantissa;
            denominator = BigInteger.One << 1074;
        }
        else
        {
            numerator = (BigInteger.One << 52) + mantissa;
            if (exp >= 0)
            {
                if (exp >= 52)
                {
                    numerator <<= (exp - 52);
                    denominator = BigInteger.One;
                }
                else
                {
                    denominator = BigInteger.One << (52 - exp);
                }
            }
            else
            {
                denominator = BigInteger.One << (52 - exp);
            }
        }

        if (negative)
            numerator = -numerator;

        // Reduce fraction
        var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
        if (gcd > BigInteger.One)
        {
            numerator /= gcd;
            denominator /= gcd;
        }

        return PyTupleObject.CreateTuple(
            new PyIntObject(numerator),
            new PyIntObject(denominator)
        );
    }

    [PyMethod("hex")]
    [PyFunctionParameters]
    private static PyResult Hex(PyCallContext context, PyFloatObject self, PyArguments arguments)
    {
        var val = self.Value;
        if (double.IsNaN(val))
            return PyStrObject.FromString("nan");
        if (double.IsInfinity(val))
            return PyStrObject.FromString(val > 0 ? "inf" : "-inf");

        long bits = BitConverter.DoubleToInt64Bits(val);
        bool negative = (bits >> 63) is not 0;
        int exp = (int)((bits >> 52) & 0x7FF);
        long mantissa = bits & 0xFFFFFFFFFFFFFL;

        string sign = negative ? "-" : string.Empty;

        // Zero special case
        if (val is 0)
            return PyStrObject.FromString($"{sign}0x0.0p+0");

        if (exp is 0)
            // Subnormal: 0.mantissa * 2^(-1022)
            return PyStrObject.FromString($"{sign}0x0.{mantissa:x013}p-1022");

        // Normalized: 1.mantissa * 2^(exp - 1023)
        int exponent = exp - 1023;
        return PyStrObject.FromString($"{sign}0x1.{mantissa:x013}p{exponent:+0;-0}");
    }

    [PyClassMethod("fromhex")]
    [PyFunctionParameters("string", "/")]
    private static PyResult FromHex(PyCallContext context, PyTypeObject cls, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject strObj)
            return PyResult.TypeError(PySR.Runtime_Float_FromHexBadArgumentType);

        if (!TryParseHexFloat(strObj.Value, out var result, out var overflow))
            return PyResult.ValueError(PySR.Runtime_Float_InvalidHexFloat);
        if (overflow)
            return PyResult.OverflowError(PySR.Runtime_Float_HexValueTooLarge);

        return PyFloatObject.FromDouble(result);
    }

    private static bool TryParseFloatString(string text, out double result)
    {
        result = 0;
        var trimmed = text.Trim().ToLowerInvariant();

        // Handle special values
        if (trimmed is "inf" or "infinity")
        {
            result = double.PositiveInfinity;
            return true;
        }
        if (trimmed is "-inf" or "-infinity")
        {
            result = double.NegativeInfinity;
            return true;
        }
        if (trimmed is "+inf" or "+infinity")
        {
            result = double.PositiveInfinity;
            return true;
        }
        if (trimmed is "nan" or "+nan" or "-nan")
        {
            result = double.NaN;
            return true;
        }

        return double.TryParse(trimmed, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out result);
    }

    // CPython float_fromhex (floatobject.c), transcribed: a [sign]
    // ['0x'] hex integer ['.' fraction] [(p|P) decimal-exponent] literal
    // where the prefix, fraction, exponent and surrounding whitespace
    // are all optional. Infinities and nans (accepted before the sign
    // in CPython) are not handled here. The value is rounded to double
    // precision half-to-even from the hex coefficient, with the p
    // exponent shifting by powers of two.
    private static bool TryParseHexFloat(string text, out double result, out bool overflow)
    {
        result = 0;
        overflow = false;
        int i = 0;
        int end = text.Length;

        while (i < end && IsPySpace(text[i]))
            i++;

        // CPython _Py_parse_inf_or_nan: an optionally signed inf/infinity
        // or nan token (ASCII case-insensitive) parses directly, before
        // the sign and coefficient grammar; trailing content still fails
        // the end-of-string check
        int tokenEnd = ParseInfOrNan(text, i, out var tokenValue);
        if (tokenEnd != i)
        {
            i = tokenEnd;
            while (i < end && IsPySpace(text[i]))
                i++;
            if (i != end)
                return false;
            result = tokenValue;
            return true;
        }

        bool negate = false;
        if (i < end && text[i] is '-')
        {
            negate = true;
            i++;
        }
        else if (i < end && text[i] is '+')
        {
            i++;
        }

        // ['0x']: '0' followed by x/X starts the prefix, otherwise the
        // '0' is the first coefficient digit
        int sStore;
        if (i < end && text[i] is '0')
        {
            i++;
            if (i < end && text[i] is 'x' or 'X')
                i++;
            else
                i--;
        }
        int coeffStart = i;

        while (i < end && HexDigitValue(text[i]) >= 0)
            i++;
        sStore = i;
        int coeffEnd;
        if (i < end && text[i] is '.')
        {
            i++;
            while (i < end && HexDigitValue(text[i]) >= 0)
                i++;
            coeffEnd = i - 1;
        }
        else
        {
            coeffEnd = i;
        }

        int ndigits = coeffEnd - coeffStart;
        int fdigits = coeffEnd - sStore;
        if (ndigits <= 0)
            return false;

        long exp;
        if (i < end && text[i] is 'p' or 'P')
        {
            i++;
            int expStart = i;
            if (i < end && text[i] is '-' or '+')
                i++;
            if (i >= end || text[i] < '0' || text[i] > '9')
                return false;
            i++;
            while (i < end && text[i] >= '0' && text[i] <= '9')
                i++;
            // strtol clamps out-of-range exponents to LONG_MAX/LONG_MIN
            if (!long.TryParse(text[expStart..i], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out exp))
                exp = text[expStart] is '-' ? long.MinValue : long.MaxValue;
        }
        else
        {
            exp = 0;
        }

        while (i < end && IsPySpace(text[i]))
            i++;
        if (i != end)
            return false;

        // CPython's HEX_DIGIT(j): the (ndigits-1-j)th coefficient digit
        // in reading order — the jth least significant, skipping the point
        long HexDigit(int j) => HexDigitValue(text[j < fdigits ? coeffEnd - j : coeffEnd - 1 - j]);

        // discard leading zeros and catch extreme overflow / underflow
        while (ndigits > 0 && HexDigit(ndigits - 1) is 0)
            ndigits--;
        if (ndigits is 0 || exp < long.MinValue / 2)
        {
            result = negate ? -0.0 : 0.0;
            return true;
        }
        if (exp > long.MaxValue / 2)
        {
            overflow = true;
            return true;
        }

        // adjust the exponent for the fractional part
        exp -= 4L * fdigits;

        // top_exp = 1 more than the exponent of the most significant bit
        long topExp = exp + 4L * (ndigits - 1);
        for (var digit = HexDigit(ndigits - 1); digit is not 0; digit /= 2)
            topExp++;

        // catch almost all nonextreme overflow and underflow here
        if (topExp < -1021 - 53)    // DBL_MIN_EXP - DBL_MANT_DIG
        {
            result = negate ? -0.0 : 0.0;
            return true;
        }
        if (topExp > 1024)          // DBL_MAX_EXP
        {
            overflow = true;
            return true;
        }

        // lsb = exponent of the least significant bit of the rounded value
        long lsb = Math.Max(topExp, -1021L) - 53;

        double x = 0.0;
        if (exp >= lsb)
        {
            // no rounding required
            for (int k = ndigits - 1; k >= 0; k--)
                x = 16.0 * x + HexDigit(k);
            result = negate ? -Math.ScaleB(x, (int)exp) : Math.ScaleB(x, (int)exp);
            return true;
        }

        // key_digit indexes the hex digit holding the first bit to round
        long halfEps = 1L << (int)((lsb - exp - 1) % 4);
        long keyDigit = (lsb - exp - 1) / 4;
        for (int k = ndigits - 1; k > keyDigit; k--)
            x = 16.0 * x + HexDigit(k);
        var key = HexDigit((int)keyDigit);
        x = 16.0 * x + (key & (16 - 2 * halfEps));

        // round-half-even: round up if the half-eps bit is set and any
        // bit below it (in this digit, the next one at half_eps == 8, or
        // any lower digit) is set
        if ((key & halfEps) is not 0)
        {
            bool roundUp = (key & (3 * halfEps - 1)) is not 0
                || (halfEps is 8 && keyDigit + 1 < ndigits && (HexDigit((int)keyDigit + 1) & 1) is not 0);
            if (!roundUp)
            {
                for (int k = (int)keyDigit - 1; k >= 0; k--)
                {
                    if (HexDigit(k) is not 0)
                    {
                        roundUp = true;
                        break;
                    }
                }
            }
            if (roundUp)
            {
                x += 2 * halfEps;
                if (topExp is 1024 && x == Math.ScaleB(2.0 * halfEps, 53))
                {
                    // pre-rounded value below 2**DBL_MAX_EXP rounds up to it
                    overflow = true;
                    return true;
                }
            }
        }
        result = Math.ScaleB(x, (int)(exp + 4 * keyDigit));
        if (negate)
            result = -result;
        return true;
    }

    // Py_ISSPACE: the six ASCII whitespace bytes
    internal static bool IsPySpace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\v' or '\f';

    // CPython _Py_parse_inf_or_nan (pystrtod.c): an optional sign, then
    // "inf" optionally extended to "infinity", or "nan" — ASCII
    // case-insensitive. Returns the position after the token, or start
    // when nothing matched.
    internal static int ParseInfOrNan(string text, int start, out double value)
    {
        int i = start;
        bool negate = false;
        if (i < text.Length && text[i] is '-')
        {
            negate = true;
            i++;
        }
        else if (i < text.Length && text[i] is '+')
        {
            i++;
        }
        if (CaseInsensitiveMatch(text, i, "inf"))
        {
            i += 3;
            if (CaseInsensitiveMatch(text, i, "inity"))
                i += 5;
            value = negate ? double.NegativeInfinity : double.PositiveInfinity;
        }
        else if (CaseInsensitiveMatch(text, i, "nan"))
        {
            i += 3;
            // the sign bit of a NaN is unobservable from Python
            value = double.NaN;
        }
        else
        {
            i = start;
            value = 0;
        }
        return i;
    }

    private static bool CaseInsensitiveMatch(string text, int pos, string pattern)
    {
        if (pos + pattern.Length > text.Length)
            return false;
        for (int k = 0; k < pattern.Length; k++)
        {
            var c = text[pos + k];
            if (c is >= 'A' and <= 'Z')
                c = (char)(c + 32);
            if (c != pattern[k])
                return false;
        }
        return true;
    }

    private static int HexDigitValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };

    [AIGenerated]
    protected override PyResult Format(PyCallContext context, PyFloatObject self, PyObject formatSpec)
    {
        if (formatSpec is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_FormatArg2NonString, formatSpec.PyType.FullName);

        // CPython _PyFloat_FormatAdvancedWriter: a zero-length spec makes
        // __format__ equivalent to str(obj) - the shortest-repr rendering,
        // not the 'g' default-precision path below
        if (str.Value.Length is 0)
            return PySpecialMethods.Str(context, self);

        if (!PyFormatSpec.TryParse(str.Value, out var spec, out var bothSeparators))
            return PyFormatSpec.ParseError(str.Value, self.PyType.FullName);

        double val = self.Value;

        // CPython format_float_internal: an omitted type behaves like repr
        // (shortest round-trip with a guaranteed decimal point) when no
        // precision is given, else like 'g' keeping the add-dot-0 behavior
        bool emptyType = spec.Type is null;
        char formatType;
        if (emptyType)
            formatType = spec.Precision is null ? 'r' : 'g';
        else
        {
            formatType = spec.Type ?? 'g';
        }
        int precision = spec.Precision ?? (emptyType ? 0 : 6);

        // CPython checks grouping compatibility after the parse, before the
        // presentation-type dispatch (",b" -> Cannot specify); an omitted
        // type is judged like 'g'
        var groupingError = PyFormatSpec.ValidateGrouping(spec, bothSeparators, spec.Type ?? 'g');
        if (groupingError.IsError)
            return groupingError;

        if (spec.CoercePositiveZero && val is 0.0 && double.IsNegative(val))
            val = 0.0;

        string text;
        bool isSpecial = !double.IsFinite(val);

        if (isSpecial)
        {
            text = double.IsNaN(val)
                ? (char.IsUpper(formatType) ? "NAN" : "nan")
                : (char.IsUpper(formatType) ? "INF" : "inf");
            // CPython appends the '%' suffix unconditionally after
            // rendering, so inf/nan keep it too (the *100 is a no-op)
            if (formatType is '%')
                text += "%";
        }
        else
        {
            var absValue = double.Abs(val);
            switch (char.ToLowerInvariant(formatType))
            {
                case 'f':
                    text = absValue.ToString($"F{precision}", CultureInfo.InvariantCulture);
                    if (spec.WidthGrouping is not null)
                        text = ApplyGrouping(text, spec.WidthGrouping.Value);
                    break;
                case 'e':
                    text = absValue.ToString($"E{precision}", CultureInfo.InvariantCulture);
                    if (formatType is 'e')
                        text = text.ToLowerInvariant();
                    // .NET 'e' pads the exponent to 3 digits (e+003); CPython
                    // format() uses at least 2 (e+03).
                    text = FixExponentWidth(text);
                    break;
                case 'g':
                case 'n':
                    {
                        // CPython: 'n' equals 'g' under the C locale; the
                        // ','/'_' grouping option is rejected with 'n'.
                        char? grouping = spec.WidthGrouping ?? spec.PrecisionGrouping;
                        if (grouping is not null && formatType is 'n')
                            return PyResult.ValueError($"Cannot specify '{grouping}' with 'n'.");

                        // Precision 0 means 1 significant digit; .NET 'g0' is
                        // the shortest round-trip form, so normalize to 1.
                        int gPrec = precision is 0 ? 1 : precision;
                        // Lowercase 'g' makes .NET emit a lowercase 'e'
                        // (CPython 'g'/'n' style); 'G' keeps uppercase 'E'.
                        string fmt = formatType is 'G' ? $"G{gPrec}" : $"g{gPrec}";
                        text = absValue.ToString(fmt, CultureInfo.InvariantCulture);

                        if (spec.AlternateForm)
                            // CPython '#' keeps trailing zeros up to the
                            // significant digits and forces a decimal point.
                            text = AddGTrailingZeros(text, gPrec);

                        // Grouping applies only to the integer part of a
                        // fixed-point representation (no 'e'/'E').
                        if (grouping is not null && !text.Contains('e') && !text.Contains('E'))
                            text = ApplyGrouping(text, grouping.Value);
                        break;
                    }
                case '%':
                    text = (absValue * 100).ToString($"F{precision}", CultureInfo.InvariantCulture);
                    if (spec.WidthGrouping is not null)
                        text = ApplyGrouping(text, spec.WidthGrouping.Value);
                    text += "%";
                    break;
                case 'r':
                    // Omitted type: repr rendering; grouping and width still
                    // apply below (scientific repr is left ungrouped). The
                    // sign is stripped here and re-added by the common
                    // prefix logic.
                    text = FormatShortestRepr(val);
                    if (text.StartsWith('-'))
                        text = text[1..];
                    if (spec.WidthGrouping is not null)
                        text = ApplyGrouping(text, spec.WidthGrouping.Value);
                    break;
                default:
                    return PyResult.ValueError(PySR.Runtime_Object_FormatUnknownCode, formatType, self.PyType.FullName);
            }

            // CPython's ADD_DOT_0 behavior for an omitted type with an
            // explicit precision: the exponent threshold is one stricter
            // than plain 'g' (decpt > precision - 1) and integer fixed-point
            // results gain a ".0" suffix.
            if (emptyType && spec.Precision is not null && !text.Contains('e') && !text.Contains('E'))
            {
                // decpt is CPython's decimal-point position: the integer
                // digit count, negative for values below 0.1
                int decpt;
                int dot = text.IndexOf('.');
                if (dot < 0)
                {
                    decpt = text.Length;
                }
                else if (text[0] is '0')
                {
                    int i = dot + 1;
                    while (i < text.Length && text[i] is '0')
                        i++;
                    decpt = -(i - dot - 1);
                }
                else
                {
                    decpt = dot;
                }

                if (decpt >= precision)
                {
                    text = FixExponentWidth(absValue.ToString($"E{precision - 1}", CultureInfo.InvariantCulture).ToLowerInvariant());
                    // CPython 'g' strips trailing mantissa zeros
                    int eIdx = text.IndexOf('e');
                    var mantissa = text[..eIdx];
                    if (mantissa.Contains('.'))
                        mantissa = mantissa.TrimEnd('0').TrimEnd('.');
                    text = mantissa + text[eIdx..];
                }
                else if (dot < 0)
                {
                    text += ".0";
                }
            }

            if (spec.AlternateForm && !text.Contains('.'))
            {
                if (text.Contains('e') || text.Contains('E'))
                {
                    int eIndex = text.IndexOfAny(['e', 'E']);
                    text = text.Insert(eIndex, ".");
                }
                else if (formatType is not '%')
                {
                    text += ".";
                }
            }
        }

        var prefix = string.Empty;
        if (double.IsNegative(val) && !double.IsNaN(val))
        {
            prefix = "-";
        }
        else if (!double.IsNaN(val))
        {
            if (spec.Sign is '+' or ' ')
                prefix = spec.Sign.Value.ToString();
        }

        if (spec.Width is not null)
        {
            var width = spec.Width.Value;
            var fill = spec.Fill ?? (spec.SignAwareZeroPadding ? '0' : ' ');
            var align = spec.Align ?? (spec.SignAwareZeroPadding ? '=' : '>');
            text = ApplyWidth(prefix, text, width, fill, align);
        }
        else
        {
            text = prefix + text;
        }

        return PyStrObject.FromString(text);

        static string ApplyGrouping(string value, char grouping)
        {
            // CPython: grouping applies only to the integer part of a
            // fixed-point representation; scientific form is left unchanged.
            int eIdx = value.IndexOf('e');
            if (eIdx < 0)
                eIdx = value.IndexOf('E');
            int dotIdx = value.IndexOf('.');
            int intEnd = dotIdx >= 0 ? dotIdx : (eIdx >= 0 ? eIdx : value.Length);
            if (intEnd <= 3)
                return value;

            var sb = new System.Text.StringBuilder();
            int first = intEnd % 3;
            if (first is 0)
                first = 3;
            sb.Append(value, 0, first);
            for (int i = first; i < intEnd; i += 3)
            {
                sb.Append(grouping);
                sb.Append(value, i, 3);
            }
            sb.Append(value, intEnd, value.Length - intEnd);
            return sb.ToString();
        }

        static string FixExponentWidth(string s)
        {
            // .NET 'e'/'E' pads the exponent to 3 digits (e+003); CPython
            // format() uses at least 2 (e+03). Drop the leading zero of a
            // 3-digit exponent below 100, keeping 3 digits for exponents >= 100.
            int marker = s.IndexOf('e');
            if (marker < 0)
                marker = s.IndexOf('E');
            if (marker < 0)
                return s;
            int signPos = marker + 1;
            if (signPos + 3 < s.Length &&
                (s[signPos] is '+' or '-') &&
                s[signPos + 1] is '0' &&
                char.IsAsciiDigit(s[signPos + 2]) &&
                char.IsAsciiDigit(s[signPos + 3]))
                return s[..(signPos + 1)] + s[(signPos + 2)..];
            return s;
        }

        static string AddGTrailingZeros(string s, int sigPrec)
        {
            // CPython '#' keeps the decimal point and pads trailing zeros so
            // the mantissa shows exactly 'sigPrec' significant digits.
            int signLen = s.Length > 0 && (s[0] is '+' or '-' or ' ') ? 1 : 0;
            string sign = s[..signLen];
            string body = s[signLen..];
            int eIdx = body.IndexOf('e');
            if (eIdx < 0)
                eIdx = body.IndexOf('E');
            string mantissa = eIdx < 0 ? body : body[..eIdx];
            string exponent = eIdx < 0 ? string.Empty : body[eIdx..];

            int zeros = sigPrec - CountSignificantDigits(mantissa);
            if (zeros > 0)
            {
                if (mantissa.Contains('.'))
                    mantissa += new string('0', zeros);
                else
                    mantissa += "." + new string('0', zeros);
            }
            else if (!mantissa.Contains('.'))
            {
                mantissa += ".";
            }
            return sign + mantissa + exponent;
        }

        static int CountSignificantDigits(string mantissa)
        {
            // Count digits after the first non-zero digit; an all-zero
            // mantissa ("0", "0.000") counts as 1 significant digit.
            int count = 0;
            bool started = false;
            foreach (char c in mantissa)
            {
                if (char.IsAsciiDigit(c))
                {
                    if (c is not '0')
                    {
                        started = true;
                        count++;
                    }
                    else if (started)
                    {
                        count++;
                    }
                }
            }
            return started ? count : 1;
        }

        static string ApplyWidth(string prefix, string body, int width, char fill, char align)
        {
            var text = prefix + body;
            if (text.Length >= width)
                return text;

            var padding = width - text.Length;
            return align switch
            {
                '<' => text.PadRight(width, fill),
                '^' => PadCenter(text, width, fill),
                '=' => prefix + body.PadLeft(body.Length + padding, fill),
                _ => text.PadLeft(width, fill),
            };
        }

        static string PadCenter(string text, int width, char fill)
        {
            var totalPadding = width - text.Length;
            var leftPadding = totalPadding / 2;
            return string.Create(width, (text, fill, leftPadding), static (span, state) =>
            {
                span[..state.leftPadding].Fill(state.fill);
                state.text.AsSpan().CopyTo(span[state.leftPadding..]);
                span[(state.leftPadding + state.text.Length)..].Fill(state.fill);
            });
        }
    }

    protected override PyResult Round(PyCallContext context, PyFloatObject self, PyObject ndigits)
    {
        if (ndigits is PyNoneObject)
        {
            // CPython float___round___impl: NaN -> ValueError, infinity ->
            // OverflowError (a bare TypeError carried no message before).
            if (double.IsNaN(self.Value))
                return PyResult.ValueError(PySR.Runtime_Float_CannotConvertNaNToInteger);
            if (double.IsInfinity(self.Value))
                return PyResult.OverflowError(PySR.Runtime_Float_CannotConvertInfinityToInteger);

            return PyIntObject.FromInteger((BigInteger)Math.Round(self.Value));
        }

        var result = PySpecialMethods.Index(context, ndigits);
        if (result.IsError)
            return result;

        var nd = result.Value;

        // NaN and infinities round to themselves (CPython float___round___impl).
        if (!double.IsFinite(self.Value))
            return self;

        // CPython boundaries: NDIGITS_MAX = (53 + 1021) * 0.30103 ~= 323,
        // NDIGITS_MIN = -(1024) * 0.30103 ~= -308.
        if (nd.Value > 323)
            return self;   // round(x, huge positive ndigits) -> x
        if (nd.Value < -308)
            return PyFloatObject.FromDouble(Math.CopySign(0.0, self.Value));   // -> +-0.0

        var value = RoundDecimalExact(self.Value, (int)nd.Value);
        // CPython double_round: a re-parse that overflows (rounded value
        // infinite while x was finite) raises OverflowError
        if (double.IsInfinity(value))
            return PyResult.OverflowError(PySR.Runtime_Float_RoundedValueTooLarge);
        if (value.Equals(self.Value))
            return self;
        return PyFloatObject.FromDouble(value);
    }

    // Rounds a finite double to ndigits decimal places using exact decimal
    // arithmetic, mirroring CPython's float_round/_Py_dg_dtoa path. Unlike
    // Math.Round (which recognises binary midpoints like 2.675), this yields
    // CPython-identical results: round(2.675, 2) == 2.67.
    private static double RoundDecimalExact(double x, int ndigits)
    {
        var bits = BitConverter.DoubleToInt64Bits(x);
        var negative = bits < 0;
        if (negative)
            bits &= ~(1L << 63);
        var expBits = (int)((bits >> 52) & 0x7FF);
        var fracBits = bits & 0xFFFFFFFFFFFFFL;

        BigInteger mantissa;
        int e2;
        if (expBits is 0)
        {
            mantissa = fracBits;
            e2 = -1074;
        }
        else
        {
            mantissa = fracBits | 0x10000000000000L;
            e2 = expBits - 1023 - 52;
        }

        BigInteger r;
        double d;
        if (ndigits >= 0)
        {
            // R = round(mantissa * 10^ndigits * 2^e2)
            var num = mantissa * BigInteger.Pow(10, ndigits);
            r = e2 >= 0 ? num << e2 : DivRoundHalfEven(num, BigInteger.One << (-e2));
            // compose through the decimal string: r itself can exceed the
            // double range even when the quotient does not, and Parse is
            // one correctly-rounded scaling, like CPython's strtod
            d = double.Parse(
                r.ToString(System.Globalization.CultureInfo.InvariantCulture) + "e" + (-ndigits).ToString(System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            // R = round(mantissa * 2^e2 / 10^|ndigits|)
            var n = -ndigits;
            var five = BigInteger.Pow(5, n);
            var k = e2 - n;
            BigInteger num, den;
            if (k >= 0)
            {
                num = mantissa << k;
                den = five;
            }
            else
            {
                num = mantissa;
                den = five << (-k);
            }
            r = DivRoundHalfEven(num, den);
            d = (double)r * Math.Pow(10, n);
        }
        return negative ? -d : d;
    }

    // BigInteger division rounded half-to-even (banker's rounding).
    private static BigInteger DivRoundHalfEven(BigInteger num, BigInteger den)
    {
        var q = BigInteger.DivRem(num, den, out var rem);
        var twice = rem << 1;
        if (twice > den || (twice == den && !q.IsEven))
            q += BigInteger.One;
        return q;
    }

    // float.__floor__/__ceil__/__trunc__ all return the exact int (CPython
    // PyLong_FromDouble); non-finite values report the conversion errors
    internal static PyResult ToRoundInt(double value)
    {
        if (double.IsInfinity(value))
            return PyResult.OverflowError(PySR.Runtime_Float_CannotConvertInfinityToInteger);
        if (double.IsNaN(value))
            return PyResult.ValueError(PySR.Runtime_Float_CannotConvertNaNToInteger);
        return PyIntObject.FromInteger((BigInteger)value);
    }

    protected override PyResult Trunc(PyCallContext context, PyFloatObject self)
    {
        return ToRoundInt(Math.Truncate(self.Value));
    }

    protected override PyResult Floor(PyCallContext context, PyFloatObject self)
    {
        return ToRoundInt(Math.Floor(self.Value));
    }

    protected override PyResult Ceil(PyCallContext context, PyFloatObject self)
    {
        return ToRoundInt(Math.Ceiling(self.Value));
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython's float_new takes at most one positional argument and no
        // keywords; an unmatched dispatcher call would only give a bare
        // TypeError, so produce the specific messages here.
        if (args.Count > 1)
            return PyResult.TypeError(PySR.Runtime_Arguments_OverflowArgs, 1, args.Count);
        if (!PyArgsValidator.ValidateEmptyKwargs(kwargs, out var kwErr))
            return kwErr.Value;

        var result = _new.Call(context, args, kwargs);
        if (result.IsError)
            return result;

        // CPython float_new: a subclass call produces a subclass instance
        // copied from the value (float_subtype_new); the conversion may
        // pass a shared exact float through, which must not be retagged
        // in place
        var obj = result.Value;
        Debug.Assert(obj is PyFloatObject);
        if (obj.PyType != cls)
        {
            obj = PyFloatObject.FromDouble(((PyFloatObject)obj).Value);
            obj._pyType = cls;
        }
        return obj;
    }
}

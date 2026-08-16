using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Globalization;
using System.Numerics;

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
    protected override PyResult Repr(PyCallContext context, PyFloatObject self)
    {
        var val = self.Value;
        if (double.IsNaN(val))
            return PyStrObject.FromString("nan");
        if (double.IsInfinity(val))
            return PyStrObject.FromString(val > 0 ? "inf" : "-inf");

        // Use "G" format for shortest representation, add ".0" for integer-valued floats
        string text = val.ToString("G", CultureInfo.InvariantCulture);
        if (!text.Contains('.') && !text.Contains('e') && !text.Contains('E'))
            text += ".0";
        return PyStrObject.FromString(text);
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
        return self.Value >= 0 ? self : Neg(context, self);
    }

    protected override PyResult Add(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyFloatObject.FromDouble(self.Value + (double)intObj.Value),
            PyFloatObject floatObj => PyFloatObject.FromDouble(self.Value + floatObj.Value),
            _ => base.Add(context, self, other),
        };
    }
    protected override PyResult Sub(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyFloatObject.FromDouble(self.Value - (double)intObj.Value),
            PyFloatObject floatObj => PyFloatObject.FromDouble(self.Value - floatObj.Value),
            _ => base.Sub(context, self, other),
        };
    }
    protected override PyResult Mul(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyFloatObject.FromDouble(self.Value * (double)intObj.Value),
            PyFloatObject floatObj => PyFloatObject.FromDouble(self.Value * floatObj.Value),
            _ => base.Mul(context, self, other),
        };
    }
    protected override PyResult TrueDiv(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => intObj.Value.IsZero ? PyResult.ZeroDivisionError() : PyFloatObject.FromDouble(self.Value / (double)intObj.Value),
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
            var dv = (double)intObj.Value;
            if (double.IsInfinity(dv))
                return PyResult.OverflowError("int too large to convert to float");
            return PyFloatObject.FromDouble(double.Floor(self.Value / dv));
        }
        if (other is PyFloatObject floatObj)
        {
            if (floatObj.Value is 0)
                return PyResult.ZeroDivisionError();
            return PyFloatObject.FromDouble(double.Floor(self.Value / floatObj.Value));
        }
        return base.FloorDiv(context, self, other);
    }
    protected override PyResult DivMod(PyCallContext context, PyFloatObject self, PyObject other)
    {
        var q = FloorDiv(context, self, other);
        if (q.IsError || q.IsNotImplemented)
            return q;
        var r = Mod(context, self, other);
        if (r.IsError || r.IsNotImplemented)
            return r;
        return PyTupleObject.CreateTuple(q.Value, r.Value);
    }
    protected override PyResult Mod(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => intObj.Value.IsZero ? PyResult.ZeroDivisionError() : PyFloatObject.FromDouble(FloatMod(self.Value, (double)intObj.Value)),
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
    protected override PyResult Pow(PyCallContext context, PyFloatObject self, PyObject other, PyObject modulo)
    {
        if (modulo is not PyNoneObject)
            return PyResult.TypeError(PySR.Runtime_Number_PowThirdArgNotInteger);
        return other switch
        {
            PyIntObject intObj => PyFloatObject.FromDouble(double.Pow(self.Value, (double)intObj.Value)),
            PyFloatObject floatObj => PyFloatObject.FromDouble(double.Pow(self.Value, floatObj.Value)),
            _ => base.Pow(context, self, other, modulo),
        };
    }
    protected override PyResult RAdd(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return Add(context, self, other);
    }
    protected override PyResult RSub(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyFloatObject.FromDouble((double)intObj.Value - self.Value),
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
            PyIntObject intObj => PyFloatObject.FromDouble((double)intObj.Value / self.Value),
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
            var dv = (double)intObj.Value;
            if (double.IsInfinity(dv))
                return PyResult.OverflowError("int too large to convert to float");
            return PyFloatObject.FromDouble(double.Floor(dv / self.Value));
        }
        if (other is PyFloatObject floatObj)
            return PyFloatObject.FromDouble(double.Floor(floatObj.Value / self.Value));
        return base.RFloorDiv(context, self, other);
    }
    protected override PyResult RDivMod(PyCallContext context, PyFloatObject self, PyObject other)
    {
        var q = RFloorDiv(context, self, other);
        if (q.IsError || q.IsNotImplemented)
            return q;
        var r = RMod(context, self, other);
        if (r.IsError || r.IsNotImplemented)
            return r;
        return PyTupleObject.CreateTuple(q.Value, r.Value);
    }
    protected override PyResult RMod(PyCallContext context, PyFloatObject self, PyObject other)
    {
        if (self.Value is 0)
            return PyResult.ZeroDivisionError();

        return other switch
        {
            PyIntObject intObj => PyFloatObject.FromDouble(FloatMod((double)intObj.Value, self.Value)),
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
            PyIntObject intObj => PyFloatObject.FromDouble(double.Pow((double)intObj.Value, self.Value)),
            PyFloatObject floatObj => PyFloatObject.FromDouble(double.Pow(floatObj.Value, self.Value)),
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
        if (!double.IsFinite(val))
            return PyResult.ValueError("cannot convert infinity/NaN to integer ratio");

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
            return PyResult.TypeError("fromhex arg must be str");

        if (!TryParseHexFloat(strObj.Value, out double result))
            return PyResult.ValueError("invalid hexadecimal floating-point string");

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

    private static bool TryParseHexFloat(string text, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim().ToLowerInvariant();
        int pos = 0;

        // Parse optional sign
        bool negative = false;
        if (pos < text.Length && text[pos] is '-')
        {
            negative = true;
            pos++;
        }
        else if (pos < text.Length && text[pos] is '+')
        {
            pos++;
        }

        // Expect "0x" prefix
        if (pos + 1 >= text.Length || text[pos] is not '0' || text[pos + 1] is not 'x')
            return false;
        pos += 2;

        if (pos >= text.Length)
            return false;

        // Parse hex digits
        var intPart = new System.Text.StringBuilder();
        var fracPart = new System.Text.StringBuilder();
        bool hasDot = false;

        while (pos < text.Length && text[pos] is not 'p')
        {
            if (text[pos] is '.')
            {
                if (hasDot)
                    return false;
                hasDot = true;
                pos++;
                continue;
            }
            if ((text[pos] >= '0' && text[pos] <= '9') || (text[pos] >= 'a' && text[pos] <= 'f'))
            {
                if (hasDot)
                    fracPart.Append(text[pos]);
                else
                    intPart.Append(text[pos]);
                pos++;
            }
            else
            {
                return false;
            }
        }

        if (intPart.Length is 0 && fracPart.Length is 0)
            return false;

        // Parse exponent
        if (pos >= text.Length || text[pos] is not 'p')
            return false;
        pos++;

        if (pos >= text.Length)
            return false;

        bool expNegative = false;
        if (text[pos] is '-')
        {
            expNegative = true;
            pos++;
        }
        else if (text[pos] is '+')
        {
            pos++;
        }

        if (pos >= text.Length || text[pos] < '0' || text[pos] > '9')
            return false;

        int exponent = 0;
        while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9')
        {
            exponent = exponent * 10 + (text[pos] - '0');
            pos++;
        }

        if (expNegative)
            exponent = -exponent;

        // Build the value
        string hexStr = (intPart.Length > 0 ? "0x" + intPart.ToString() : "0x0") +
                        (fracPart.Length > 0 ? "." + fracPart.ToString() : string.Empty);

        // Parse as hex float: value = hex_digits * 2^exponent
        // Convert to integer mantissa first
        string fullHex = intPart.ToString() + fracPart.ToString();
        if (fullHex.Length is 0)
            fullHex = "0";

        if (!long.TryParse(fullHex, System.Globalization.NumberStyles.HexNumber, null, out long mantissa))
            return false;

        int power = exponent - fracPart.Length * 4; // each hex digit = 4 bits

        if (power >= 0)
            result = mantissa * Math.Pow(2, power);
        else
            result = mantissa / Math.Pow(2, -power);

        if (negative)
            result = -result;

        return true;
    }

    [AIGenerated]
    protected override PyResult Format(PyCallContext context, PyFloatObject self, PyObject formatSpec)
    {
        if (formatSpec is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_FormatArg2NonString, formatSpec.PyType.FullName);

        if (!PyFormatSpec.TryParse(str.Value, out var spec))
            return PyResult.ValueError(PySR.Runtime_Object_FormatSpecInvalid, str.Value, self.PyType.FullName);

        double val = self.Value;
        var formatType = spec.Type ?? 'g';
        int precision = spec.Precision ?? 6;

        if (spec.CoercePositiveZero && val is 0.0 && double.IsNegative(val))
            val = 0.0;

        string text;
        bool isSpecial = !double.IsFinite(val);

        if (isSpecial)
        {
            text = double.IsNaN(val)
                ? (char.IsUpper(formatType) ? "NAN" : "nan")
                : (char.IsUpper(formatType) ? "INF" : "inf");
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
                default:
                    return PyResult.ValueError(PySR.Runtime_Object_FormatUnsupported, self.PyType.FullName);
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
            if (!double.IsFinite(self.Value))
                return PyResult.TypeError(null);

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
            d = (double)r / Math.Pow(10, ndigits);
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

    protected override PyResult Trunc(PyCallContext context, PyFloatObject self)
    {
        var value = Math.Truncate(self.Value);
        if (value.Equals(self.Value))
            return self;
        return PyFloatObject.FromDouble(value);
    }

    protected override PyResult Floor(PyCallContext context, PyFloatObject self)
    {
        var value = Math.Floor(self.Value);
        if (value.Equals(self.Value))
            return self;
        return PyFloatObject.FromDouble(value);
    }

    protected override PyResult Ceil(PyCallContext context, PyFloatObject self)
    {
        var value = Math.Ceiling(self.Value);
        if (value.Equals(self.Value))
            return self;
        return PyFloatObject.FromDouble(value);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateSinglePositionalArg(args, kwargs, out var err))
            return err.Value;

        // TODO: this is temp fix
        if (args[0] is PyStrObject { Value: var str })
        {
            if (!TryParseFloatString(str, out var value))
                return PyResult.TypeError(null);

            return PyFloatObject.FromDouble(value);
        }

        return PySpecialMethods.Float(context, args[0]);
    }
}

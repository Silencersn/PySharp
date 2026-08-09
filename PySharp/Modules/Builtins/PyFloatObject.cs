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
        return PyIntObject.FromInteger(self.Value.GetHashCode());
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
        return other switch
        {
            PyIntObject intObj => intObj.Value.IsZero ? PyResult.ZeroDivisionError() : PyFloatObject.FromDouble(double.Floor(self.Value / (double)intObj.Value)),
            PyFloatObject floatObj => floatObj.Value is 0 ? PyResult.ZeroDivisionError() : PyFloatObject.FromDouble(double.Floor(self.Value / floatObj.Value)),
            _ => base.FloorDiv(context, self, other),
        };
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
            PyIntObject intObj => intObj.Value.IsZero ? PyResult.ZeroDivisionError() : PyFloatObject.FromDouble(self.Value % (double)intObj.Value),
            PyFloatObject floatObj => floatObj.Value is 0 ? PyResult.ZeroDivisionError() : PyFloatObject.FromDouble(self.Value % floatObj.Value),
            _ => base.Mod(context, self, other),
        };
    }
    protected override PyResult Pow(PyCallContext context, PyFloatObject self, PyObject other, PyObject modulo)
    {
        if (modulo is not PyNoneObject)
            return PyNotImplementedObject.NotImplemented;
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

        return other switch
        {
            PyIntObject intObj => PyFloatObject.FromDouble(double.Floor((double)intObj.Value / self.Value)),
            PyFloatObject floatObj => PyFloatObject.FromDouble(double.Floor(floatObj.Value / self.Value)),
            _ => base.RFloorDiv(context, self, other),
        };
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
            PyIntObject intObj => PyFloatObject.FromDouble((double)intObj.Value % self.Value),
            PyFloatObject floatObj => PyFloatObject.FromDouble(floatObj.Value % self.Value),
            _ => base.RMod(context, self, other),
        };
    }
    protected override PyResult RPow(PyCallContext context, PyFloatObject self, PyObject other, PyObject modulo)
    {
        if (modulo is not PyNoneObject)
            return PyNotImplementedObject.NotImplemented;
        return other switch
        {
            PyIntObject intObj => PyFloatObject.FromDouble(double.Pow((double)intObj.Value, self.Value)),
            PyFloatObject floatObj => PyFloatObject.FromDouble(double.Pow(floatObj.Value, self.Value)),
            _ => base.RPow(context, self, other, modulo),
        };
    }
    protected override PyResult Lt(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(self.Value < (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(self.Value < floatObj.Value),
            _ => base.Lt(context, self, other),
        };
    }
    protected override PyResult Gt(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(self.Value > (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(self.Value > floatObj.Value),
            _ => base.Gt(context, self, other),
        };
    }
    protected override PyResult Le(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(self.Value <= (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(self.Value <= floatObj.Value),
            _ => base.Le(context, self, other),
        };
    }
    protected override PyResult Ge(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(self.Value >= (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(self.Value >= floatObj.Value),
            _ => base.Ge(context, self, other),
        };
    }
    protected override PyResult Eq(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => PyBoolObject.FromBoolean(self.Value == (double)intObj.Value),
            PyFloatObject floatObj => PyBoolObject.FromBoolean(self.Value == floatObj.Value),
            _ => base.Eq(context, self, other),
        };
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
        return PyFloatObject.FromDouble(0.0);
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
                    break;
                case 'g':
                case 'n':
                    string fmt = formatType is 'n' ? $"N{precision}" : $"G{precision}";
                    text = absValue.ToString(fmt, CultureInfo.InvariantCulture);

                    if (formatType is 'g' or 'G')
                    {
                        if (!spec.AlternateForm && text.Contains('.'))
                            text = text.TrimEnd('0').TrimEnd('.');

                        if (text is "0")
                            text = "0.0";
                    }

                    if (spec.WidthGrouping is '_')
                        text = text.Replace(',', '_');
                    break;
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
            var fill = spec.Fill ?? (spec.SignAwareZeroPadding && spec.Align is null ? '0' : ' ');
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
            if (grouping is not ',')
                return value.Replace(',', '_');
            return value;
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

        if (nd.Value < 0)
        {
            if (!nd.IsInt32)
                return PyFloatObject.Zero;   // round(x, huge negative) -> 0.0
            var digits = nd.Int32Value;
            var factor = Math.Pow(10, -digits);
            var value = Math.Round(self.Value / factor) * factor;
            if (value.Equals(self.Value))
                return self;
            return PyFloatObject.FromDouble(value);
        }
        else if (!nd.IsInt32)
        {
            return self;   // round(x, huge positive ndigits) -> x
        }
        else if (nd.Int32Value > 15)
        {
            return self;
        }
        else
        {
            var digits = nd.Int32Value;
            var value = Math.Round(self.Value, digits);
            if (value.Equals(self.Value))
                return self;
            return PyFloatObject.FromDouble(value);
        }
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

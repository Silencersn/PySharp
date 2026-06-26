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


    public double Value { get; set; }
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
        return PyStrObject.FromString(self.Value.ToString());
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

        var digits = result.Value.Int32Value;

        if (digits < 0)
        {
            var factor = Math.Pow(10, -digits);
            var value = Math.Round(self.Value / factor) * factor;
            if (value.Equals(self.Value))
                return self;
            return PyFloatObject.FromDouble(value);
        }
        else if (digits > 15)
        {
            return self;
        }
        else
        {
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
            if (!double.TryParse(str, out var value))
                return PyResult.TypeError(null);

            return PyFloatObject.FromDouble(value);
        }

        return PySpecialMethods.Float(context, args[0]);
    }
}

using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Numerics;

namespace PySharp.PyModules.Builtins;

public class PyFloatObject : PyObject
{
    public double Value { get; set; }
    public override PyTypeObject DefaultPyType => PyFloatObjectType.Shared;

    public PyFloatObject() { }
    public PyFloatObject(double value) : this() { Value = value; }
    public static PyFloatObject FromDouble(double value)
    {
        return new PyFloatObject(value);
    }
}

public sealed class PyFloatObjectType : PyTypeObject<PyFloatObjectType, PyFloatObject>
{
    public override string Module => "builtins";
    public override string Name => "float";

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
            PyIntObject intObj => intObj.Value.IsZero ? PyResult.RaiseZeroDivisionError("float division by zero") : PyFloatObject.FromDouble(self.Value / (double)intObj.Value),
            PyFloatObject floatObj => floatObj.Value is 0 ? PyResult.RaiseZeroDivisionError("float division by zero") : PyFloatObject.FromDouble(self.Value / floatObj.Value),
            _ => base.TrueDiv(context, self, other),
        };
    }
    protected override PyResult FloorDiv(PyCallContext context, PyFloatObject self, PyObject other)
    {
        return other switch
        {
            PyIntObject intObj => intObj.Value.IsZero ? PyResult.RaiseZeroDivisionError("float floor division by zero") : PyFloatObject.FromDouble(double.Floor(self.Value / (double)intObj.Value)),
            PyFloatObject floatObj => floatObj.Value is 0 ? PyResult.RaiseZeroDivisionError("float floor division by zero") : PyFloatObject.FromDouble(double.Floor(self.Value / floatObj.Value)),
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
            PyIntObject intObj => intObj.Value.IsZero ? PyResult.RaiseZeroDivisionError("float modulo") : PyFloatObject.FromDouble(self.Value % (double)intObj.Value),
            PyFloatObject floatObj => floatObj.Value is 0 ? PyResult.RaiseZeroDivisionError("float modulo") : PyFloatObject.FromDouble(self.Value % floatObj.Value),
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
            return PyResult.RaiseZeroDivisionError("float division by zero");

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
            return PyResult.RaiseZeroDivisionError("float floor division by zero");

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
            return PyResult.RaiseZeroDivisionError("float modulo");

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
    protected override PyResult Format(PyCallContext context, PyFloatObject self, PyObject formatSpec)
    {
        if (formatSpec is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_FormatArg2NonString, formatSpec.PyType.FullName);

        if (!PyFormatSpec.TryParse(str.Value, out var spec))
            return PyResult.ValueError(PySR.Runtime_Object_FormatSpecInvalid, str.Value, self.PyType.FullName);

        int precision = spec.Precision ?? 6;
        string text;
        if (!double.IsNormal(self.Value))
        {
            if (double.IsInfinity(self.Value))
                text = spec.Type is 'f' ? "inf" : "INF";
            else
                text = spec.Type is 'f' ? "nan" : "NAN";
        }
        else
        {
            switch (spec.Type)
            {
                case 'f' or 'F':
                    Span<char> format = stackalloc char[2 + 2 + precision];
                    var offset = 0;
                    if (spec.WidthGrouping is not null)
                    {
                        "#,".CopyTo(format[offset..]);
                        offset += 2;
                    }
                    "0.".CopyTo(format[offset..]);
                    offset += 2;
                    format.Slice(offset, precision).Fill('0');
                    offset += precision;
                    text = double.Abs(self.Value).ToString(format[..offset].ToString());
                    if (spec.WidthGrouping is '_')
                        text = text.Replace(',', '_');
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        if (spec.Width is not null && spec.Align is '=')
            throw new NotImplementedException();
        if (self.Value < 0 && !double.IsNaN(self.Value))
            text = '-' + text;
        else if (spec.Sign is '+' or ' ')
            text = spec.Sign + text;
        if (spec.Width is not null)
        {
            var width = spec.Width.Value;
            if (spec.Align is '<')
                text = text.PadRight(width, spec.Fill ?? ' ');
            else if (spec.Align is '^')
                throw new NotImplementedException();
            else
                text = text.PadLeft(width, spec.Fill ?? ' ');
        }
        return PyStrObject.FromString(text);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateSinglePositionalArg(args, kwargs, out var err))
            return err.Value;
        return PySpecialMethods.Float(context, args[0]);
    }
}
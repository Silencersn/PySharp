using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Numerics;

using PySharp.Utility;

namespace PySharp.Modules.Builtins;

// TODO: AI Generated, need review

public class PyComplexObject : PyObject
{
    public Complex Value { get; }
    public override PyTypeObject DefaultPyType => PyComplexObjectType.Shared;

    public double Real => Value.Real;
    public double Imag => Value.Imaginary;

    private PyComplexObject(Complex value) { Value = value; }
    public static PyComplexObject FromComplex(Complex value) => new(value);
    public static PyComplexObject FromRealImag(double real, double imag = 0) => new(new Complex(real, imag));

    /// <summary>
    /// Parse a complex number from a string like "2j", "3.5j", "1_000j".
    /// </summary>
    public static PyComplexObject FromString(ReadOnlySpan<char> value)
    {
        if (!value.EndsWith('j'))
            throw new FormatException("Complex number must end with 'j'");

        value = value[..^1]; // strip 'j'

        if (value.Contains('_'))
        {
            var builder = new System.Text.StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (c is not '_')
                    builder.Append(c);
            }
            var imag = double.Parse(builder.ToString());
            return FromRealImag(0, imag);
        }

        var imagValue = double.Parse(value);
        return FromRealImag(0, imagValue);
    }
}

[PyType("complex")]
public sealed partial class PyComplexObjectType : PyTypeObject<PyComplexObject>
{

    protected override PyResult Repr(PyCallContext context, PyComplexObject self)
    {
        // CPython complex_repr: a +0.0 real part drops the parens and the real
        // part; inside the parens the imaginary part carries an explicit sign.
        string lead, realPart, imagPart, tail;
        if (self.Value.Real is 0.0 && !double.IsNegative(self.Value.Real))
        {
            lead = string.Empty;
            realPart = string.Empty;
            imagPart = FormatPart(self.Value.Imaginary, withSign: false);
            tail = string.Empty;
        }
        else
        {
            lead = "(";
            realPart = FormatPart(self.Value.Real, withSign: false);
            imagPart = FormatPart(self.Value.Imaginary, withSign: true);
            tail = ")";
        }
        return PyStrObject.FromString($"{lead}{realPart}{imagPart}j{tail}");
    }

    // CPython formats the parts with PyOS_double_to_string('r', 0): float repr
    // digits without the trailing ".0"; Py_DTSF_SIGN prepends '+' to
    // non-negative values ("+3", "+inf", "+nan"), the value's own sign wins.
    private static string FormatPart(double value, bool withSign)
    {
        if (double.IsNaN(value))
            return withSign ? "+nan" : "nan";
        if (double.IsInfinity(value))
            return value < 0 ? "-inf" : withSign ? "+inf" : "inf";

        var text = PyFloatObjectType.FormatShortestRepr(value);
        if (text.EndsWith(".0", StringComparison.Ordinal))
            text = text[..^2];
        if (withSign && !text.StartsWith('-'))
            text = $"+{text}";
        return text;
    }

    protected override PyResult Abs(PyCallContext context, PyComplexObject self)
    {
        // CPython complex_abs: hypot as a float; OverflowError when it
        // overflows for finite components.
        var result = double.Hypot(self.Value.Real, self.Value.Imaginary);
        if (double.IsInfinity(result) && double.IsFinite(self.Value.Real) && double.IsFinite(self.Value.Imaginary))
            return PyResult.OverflowError(PySR.Runtime_Complex_AbsoluteValueTooLarge);
        return PyFloatObject.FromDouble(result);
    }

    protected override PyResult Hash(PyCallContext context, PyComplexObject self)
    {
        int hash = HashCode.Combine(self.Value.Real, self.Value.Imaginary);
        return PyIntObject.FromInteger(hash);
    }

    protected override PyResult Bool(PyCallContext context, PyComplexObject self)
    {
        return PyBoolObject.FromBoolean(self.Value.Real is not 0 || self.Value.Imaginary is not 0);
    }

    protected override PyResult Int(PyCallContext context, PyComplexObject self)
    {
        if (self.Value.Imaginary is not 0)
            return PyResult.TypeError(null);
        return PyIntObject.FromInteger((BigInteger)self.Value.Real);
    }

    protected override PyResult Float(PyCallContext context, PyComplexObject self)
    {
        if (self.Value.Imaginary is not 0)
            return PyResult.TypeError(null);
        return PyFloatObject.FromDouble(self.Value.Real);
    }

    protected override PyResult Add(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
            return PyComplexObject.FromComplex(self.Value + c.Value);
        if (other is PyIntObject i)
            return PyComplexObject.FromComplex(self.Value + new Complex(i.Value.ToDoubleRounded(), 0));
        if (other is PyFloatObject f)
            return PyComplexObject.FromComplex(self.Value + new Complex(f.Value, 0));
        return base.Add(context, self, other);
    }
    protected override PyResult Sub(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
            return PyComplexObject.FromComplex(self.Value - c.Value);
        if (other is PyIntObject i)
            return PyComplexObject.FromComplex(self.Value - new Complex(i.Value.ToDoubleRounded(), 0));
        if (other is PyFloatObject f)
            return PyComplexObject.FromComplex(self.Value - new Complex(f.Value, 0));
        return base.Sub(context, self, other);
    }
    protected override PyResult Mul(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
            return PyComplexObject.FromComplex(self.Value * c.Value);
        if (other is PyIntObject i)
            return PyComplexObject.FromComplex(self.Value * new Complex(i.Value.ToDoubleRounded(), 0));
        if (other is PyFloatObject f)
            return PyComplexObject.FromComplex(self.Value * new Complex(f.Value, 0));
        return base.Mul(context, self, other);
    }
    protected override PyResult TrueDiv(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
        {
            if (c.Value == System.Numerics.Complex.Zero)
                return PyResult.ZeroDivisionError();
            return PyComplexObject.FromComplex(self.Value / c.Value);
        }
        if (other is PyIntObject i)
        {
            double v = i.Value.ToDoubleRounded();
            if (v is 0)
                return PyResult.ZeroDivisionError();
            return PyComplexObject.FromComplex(self.Value / new Complex(v, 0));
        }
        if (other is PyFloatObject f)
        {
            if (f.Value is 0)
                return PyResult.ZeroDivisionError();
            return PyComplexObject.FromComplex(self.Value / new Complex(f.Value, 0));
        }
        return base.TrueDiv(context, self, other);
    }
    protected override PyResult Eq(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
            return PyBoolObject.FromBoolean(self.Value == c.Value);
        if (other is PyIntObject i)
            return PyBoolObject.FromBoolean(self.Value == new Complex(i.Value.ToDoubleRounded(), 0));
        if (other is PyFloatObject f)
            return PyBoolObject.FromBoolean(self.Value == new Complex(f.Value, 0));
        return base.Eq(context, self, other);
    }
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython actual_complex_new: with no keywords and at most one
        // positional argument the constructor converts a single value;
        // otherwise the 'real'/'imag' parameters are bound and combined
        // (complex_new_impl).
        // CPython's clinic parser rejects a total argument count above the
        // positional capacity before any keyword binding.
        if (args.Count + kwargs.Count > 2)
            return PyResult.TypeError(PySR.Runtime_Complex_TakesAtMostTwoArgs, args.Count + kwargs.Count);

        if (kwargs.Count is 0 && args.Count <= 1)
            return CreateFromSingleValue(context, cls, args);

        PyObject? realArg = args.Count > 0 ? args[0] : null;
        PyObject? imagArg = args.Count > 1 ? args[1] : null;
        foreach (var (name, value) in kwargs)
        {
            switch (name)
            {
                case "real" when realArg is null:
                    realArg = value;
                    break;
                case "imag" when imagArg is null:
                    imagArg = value;
                    break;
                case "real" or "imag":
                    return PyResult.TypeError(PySR.Runtime_Complex_GivenByNameAndPosition, name, name is "real" ? 1 : 2);
                default:
                    return PyResult.TypeError(PySR.Runtime_Complex_UnexpectedKeyword, name);
            }
        }

        var realResult = ToComponent(context, realArg, PySR.Runtime_Complex_RealMustBeRealNumber);
        if (realResult.IsError)
            return realResult;

        PyResult<PyComplexObject> imagResult;
        if (imagArg is null)
            imagResult = PyComplexObject.FromRealImag(0, 0);
        else
            imagResult = ToComponent(context, imagArg, PySR.Runtime_Complex_ImagMustBeRealNumber);
        if (imagResult.IsError)
            return imagResult;

        // CPython keeps deprecated combining arithmetic for complex parts
        // (real=2j stays accepted for now), so warn instead of rejecting.
        if (realArg is PyComplexObject)
        {
            var warnResult = context.Warn(PyDeprecationWarningObjectType.Shared, PySR.Format(PySR.Runtime_Complex_RealMustBeRealNumber, "complex"));
            if (warnResult.IsError)
                return warnResult;
        }
        if (imagArg is PyComplexObject)
        {
            var warnResult = context.Warn(PyDeprecationWarningObjectType.Shared, PySR.Format(PySR.Runtime_Complex_ImagMustBeRealNumber, "complex"));
            if (warnResult.IsError)
                return warnResult;
        }

        var cr = realResult.Value.Value;
        var ci = imagResult.Value.Value;
        var real = cr.Real;
        var imag = ci.Real;
        if (imagArg is null)
        {
            // No imag argument: CPython takes the imaginary part from the
            // real component (ci.real = cr.imag).
            imag = cr.Imaginary;
        }
        else
        {
            if (imagArg is PyComplexObject)
                real -= ci.Imaginary;
            // Add the real component's imaginary part last so a -0.0
            // imaginary argument keeps its sign (0.0 + -0.0 is +0.0).
            if (realArg is PyComplexObject)
                imag += cr.Imaginary;
        }

        var obj = PyComplexObject.FromRealImag(real, imag);
        obj._pyType = cls;
        return obj;
    }

    private static PyResult CreateFromSingleValue(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args)
    {
        if (args.Count is 0)
            return CreateOfType(cls, 0, 0);

        // An exact complex with the exact type is returned unchanged.
        if (args[0] is PyComplexObject exact &&
            ReferenceEquals(exact.PyType, PyComplexObjectType.Shared) &&
            ReferenceEquals(cls, PyComplexObjectType.Shared))
            return exact;

        var result = ToComponent(context, args[0], PySR.Runtime_Complex_ArgMustBeStringOrNumber);
        if (result.IsError)
            return result;

        var value = result.Value.Value;
        return CreateOfType(cls, value.Real, value.Imaginary);
    }

    private static PyResult CreateOfType(PyTypeObject cls, double real, double imag)
    {
        var obj = PyComplexObject.FromRealImag(real, imag);
        obj._pyType = cls;
        return obj;
    }

    // One constructor component: a complex value passes through with both
    // parts; anything float- or index-able contributes its value as the real
    // part, mirroring CPython's nb_float/nb_index acceptance.
    private static PyResult<PyComplexObject> ToComponent(PyCallContext context, PyObject? arg, string errorMessage)
    {
        switch (arg)
        {
            case null:
                return PyComplexObject.FromRealImag(0, 0);
            case PyComplexObject complex:
                return complex;
            case PyFloatObject floatObject:
                return PyComplexObject.FromRealImag(floatObject.Value, 0);
            case PyIntObject intObject:
                return PyComplexObject.FromRealImag(intObject.Value.ToDoubleRounded(), 0);
        }

        var index = PySpecialMethods.Index(context, arg);
        if (index.IsError)
            return PyResult.TypeError(errorMessage, arg.PyType.Name);
        return PyComplexObject.FromRealImag(index.Value.Value.ToDoubleRounded(), 0);
    }
}

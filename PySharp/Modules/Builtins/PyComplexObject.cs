using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Globalization;
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
            var imag = double.Parse(builder.ToString(), CultureInfo.InvariantCulture);
            return FromRealImag(0, imag);
        }

        var imagValue = double.Parse(value, CultureInfo.InvariantCulture);
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
        return PyIntObject.FromInteger(PyHash.HashComplex(self.Value.Real, self.Value.Imaginary, self));
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

    protected override PyResult Neg(PyCallContext context, PyComplexObject self)
    {
        return PyComplexObject.FromComplex(-self.Value);
    }

    protected override PyResult Pos(PyCallContext context, PyComplexObject self)
    {
        // CPython complex_pos: an exact instance returns itself, a
        // subclass instance converts to an exact complex copy
        return self.PyType == PyComplexObjectType.Shared ? self : PyComplexObject.FromComplex(self.Value);
    }

    protected override PyResult Add(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
            return PyComplexObject.FromComplex(self.Value + c.Value);
        if (other is PyIntObject i)
        {
            if (!i.Value.TryToDoubleRounded(out var d))
                return PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
            return PyComplexObject.FromComplex(self.Value + new Complex(d, 0));
        }
        if (other is PyFloatObject f)
            return PyComplexObject.FromComplex(self.Value + new Complex(f.Value, 0));
        return base.Add(context, self, other);
    }
    protected override PyResult Sub(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
            return PyComplexObject.FromComplex(self.Value - c.Value);
        if (other is PyIntObject i)
        {
            if (!i.Value.TryToDoubleRounded(out var d))
                return PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
            return PyComplexObject.FromComplex(self.Value - new Complex(d, 0));
        }
        if (other is PyFloatObject f)
            return PyComplexObject.FromComplex(self.Value - new Complex(f.Value, 0));
        return base.Sub(context, self, other);
    }
    protected override PyResult Mul(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
            return PyComplexObject.FromComplex(ComplexProduct(self.Value, c.Value));
        if (other is PyIntObject i)
        {
            if (!i.Value.TryToDoubleRounded(out var d))
                return PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
            return PyComplexObject.FromComplex(self.Value * new Complex(d, 0));
        }
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
            return PyComplexObject.FromComplex(ComplexQuot(self.Value, c.Value));
        }
        if (other is PyIntObject i)
        {
            if (!i.Value.TryToDoubleRounded(out var v))
                return PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
            if (v is 0)
                return PyResult.ZeroDivisionError();
            return PyComplexObject.FromComplex(ComplexCrQuot(self.Value, v));
        }
        if (other is PyFloatObject f)
        {
            if (f.Value is 0)
                return PyResult.ZeroDivisionError();
            return PyComplexObject.FromComplex(ComplexCrQuot(self.Value, f.Value));
        }
        return base.TrueDiv(context, self, other);
    }
    protected override PyResult Pow(PyCallContext context, PyComplexObject self, PyObject other, PyObject modulo)
    {
        // CPython complex_pow: the exponent converts first (a non-number
        // yields NotImplemented), then a non-None modulo is rejected.
        if (!TryToRealComplex(other, out var exponent, out var error))
            return error ?? base.Pow(context, self, other, modulo);
        if (modulo is not PyNoneObject)
            return PyResult.ValueError(PySR.Runtime_Complex_Modulo);
        return ComplexPower(self.Value, exponent);
    }
    protected override PyResult RAdd(PyCallContext context, PyComplexObject self, PyObject other)
    {
        // Addition is commutative, so the reflected form reuses Add.
        return Add(context, self, other);
    }
    protected override PyResult RSub(PyCallContext context, PyComplexObject self, PyObject other)
    {
        // CPython complex_rsub computes other - self.
        if (!TryToRealComplex(other, out var value, out var error))
            return error ?? base.RSub(context, self, other);
        return PyComplexObject.FromComplex(value - self.Value);
    }
    protected override PyResult RMul(PyCallContext context, PyComplexObject self, PyObject other)
    {
        // Multiplication is commutative, so the reflected form reuses Mul.
        return Mul(context, self, other);
    }
    protected override PyResult RTrueDiv(PyCallContext context, PyComplexObject self, PyObject other)
    {
        // CPython complex_rdiv computes other / self: the zero check applies
        // to the complex denominator, after the other operand converts.
        if (!TryToRealComplex(other, out var numerator, out var error))
            return error ?? base.RTrueDiv(context, self, other);
        if (self.Value == System.Numerics.Complex.Zero)
            return PyResult.ZeroDivisionError();
        if (other is PyComplexObject)
            return PyComplexObject.FromComplex(ComplexQuot(numerator, self.Value));
        return PyComplexObject.FromComplex(ComplexRcQuot(numerator.Real, self.Value));
    }
    protected override PyResult RPow(PyCallContext context, PyComplexObject self, PyObject other, PyObject modulo)
    {
        // CPython complex_rpow delegates to complex_pow(other, self): the
        // complex operand supplies the exponent.
        if (!TryToRealComplex(other, out var baseValue, out var error))
            return error ?? base.RPow(context, self, other, modulo);
        if (modulo is not PyNoneObject)
            return PyResult.ValueError(PySR.Runtime_Complex_Modulo);
        return ComplexPower(baseValue, self.Value);
    }

    // CPython TO_COMPLEX/real_to_double: a complex operand passes through
    // with both parts, a float or int (index-able) value contributes its
    // value as the real part; anything else fails the conversion.
    private static bool TryToRealComplex(PyObject operand, out System.Numerics.Complex value, out PyResult? error)
    {
        error = null;
        switch (operand)
        {
            case PyComplexObject complex:
                value = complex.Value;
                return true;
            case PyFloatObject floatObject:
                value = new Complex(floatObject.Value, 0);
                return true;
            case PyIntObject intObject:
                if (!intObject.Value.TryToDoubleRounded(out var real))
                {
                    error = PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
                    value = default;
                    return false;
                }
                value = new Complex(real, 0);
                return true;
            default:
                value = default;
                return false;
        }
    }

    private static PyResult ComplexPower(System.Numerics.Complex baseValue, System.Numerics.Complex exponent)
    {
        // CPython complex_pow: an exponent that is a small exact integer
        // switches to repeated squaring (bit-exact products); anything else
        // uses the c_pow log/exp form.
        if (exponent.Imaginary is 0.0 && exponent.Real == double.Floor(exponent.Real) && double.Abs(exponent.Real) <= 100.0)
        {
            var n = (long)exponent.Real;
            var positive = ComplexPowu(baseValue, n > 0 ? n : -n);
            if (n <= 0)
            {
                // CPython c_powi divides one by the positive power; an
                // underflowed (zero) denominator is the EDOM case.
                if (positive == System.Numerics.Complex.Zero)
                    return PyResult.ZeroDivisionError(PySR.Runtime_Complex_ZeroToNegativeOrComplexPower);
                positive = ComplexQuot(System.Numerics.Complex.One, positive);
            }
            return FinitePowerResult(positive);
        }

        if (baseValue.Real is 0.0 && baseValue.Imaginary is 0.0 &&
            (exponent.Imaginary is not 0.0 || exponent.Real < 0))
            return PyResult.ZeroDivisionError(PySR.Runtime_Complex_ZeroToNegativeOrComplexPower);

        return FinitePowerResult(ComplexPow(baseValue, exponent));
    }

    private static PyResult FinitePowerResult(Complex value)
    {
        // _Py_ADJUST_ERANGE2 only flags overflow (an infinite component);
        // underflow is silently accepted.
        if (double.IsInfinity(value.Real) || double.IsInfinity(value.Imaginary))
            return PyResult.OverflowError(PySR.Runtime_Complex_ExponentiationOverflow);
        return PyComplexObject.FromComplex(value);
    }

    private static System.Numerics.Complex ComplexPowu(System.Numerics.Complex x, long n)
    {
        // CPython c_powu: binary exponentiation with c_prod products.
        var r = System.Numerics.Complex.One;
        var p = x;
        long mask = 1;
        while (mask > 0 && n >= mask)
        {
            if ((n & mask) is not 0)
                r = ComplexProduct(r, p);
            mask <<= 1;
            p = ComplexProduct(p, p);
        }
        return r;
    }

    private static System.Numerics.Complex ComplexProduct(System.Numerics.Complex z, System.Numerics.Complex w)
    {
        // CPython _Py_c_prod: naive products first, then the C11 Annex
        // G.5.1 recovery that re-derives infinities when both parts came
        // out NaN (the .NET Complex product lacks that recovery).
        double a = z.Real, b = z.Imaginary, c = w.Real, d = w.Imaginary;
        double ac = a * c, bd = b * d, ad = a * d, bc = b * c;
        var r = new Complex(ac - bd, ad + bc);
        if (double.IsNaN(r.Real) && double.IsNaN(r.Imaginary))
        {
            var recalc = false;
            if (double.IsInfinity(a) || double.IsInfinity(b))
            {
                // Box the infinity and zero out a NaN in the other factor.
                a = double.CopySign(double.IsInfinity(a) ? 1.0 : 0.0, a);
                b = double.CopySign(double.IsInfinity(b) ? 1.0 : 0.0, b);
                if (double.IsNaN(c))
                    c = double.CopySign(0.0, c);
                if (double.IsNaN(d))
                    d = double.CopySign(0.0, d);
                recalc = true;
            }
            if (double.IsInfinity(c) || double.IsInfinity(d))
            {
                c = double.CopySign(double.IsInfinity(c) ? 1.0 : 0.0, c);
                d = double.CopySign(double.IsInfinity(d) ? 1.0 : 0.0, d);
                if (double.IsNaN(a))
                    a = double.CopySign(0.0, a);
                if (double.IsNaN(b))
                    b = double.CopySign(0.0, b);
                recalc = true;
            }
            if (!recalc && (double.IsInfinity(ac) || double.IsInfinity(bd) || double.IsInfinity(ad) || double.IsInfinity(bc)))
            {
                // Recover infinities from overflow by changing NaNs to 0.
                if (double.IsNaN(a))
                    a = double.CopySign(0.0, a);
                if (double.IsNaN(b))
                    b = double.CopySign(0.0, b);
                if (double.IsNaN(c))
                    c = double.CopySign(0.0, c);
                if (double.IsNaN(d))
                    d = double.CopySign(0.0, d);
                recalc = true;
            }
            if (recalc)
                r = new(double.PositiveInfinity * (a * c - b * d), double.PositiveInfinity * (a * d + b * c));
        }
        return r;
    }

    // CPython _Py_c_quot: Smith's algorithm; NaN results recover their
    // infinities per C11 Annex G.5.2. Callers reject a zero denominator
    // first (the EDOM case).
    private static System.Numerics.Complex ComplexQuot(System.Numerics.Complex a, System.Numerics.Complex b)
    {
        double ar = a.Real, ai = a.Imaginary, br = b.Real, bi = b.Imaginary;
        double absBr = br < 0 ? -br : br;
        double absBi = bi < 0 ? -bi : bi;
        double real, imag;
        if (absBr >= absBi)
        {
            var ratio = bi / br;
            var denom = br + bi * ratio;
            real = (ar + ai * ratio) / denom;
            imag = (ai - ar * ratio) / denom;
        }
        else if (absBi >= absBr)
        {
            var ratio = br / bi;
            var denom = br * ratio + bi;
            real = (ar * ratio + ai) / denom;
            imag = (ai * ratio - ar) / denom;
        }
        else
        {
            // At least one of the denominator parts is a NaN.
            real = imag = double.NaN;
        }
        if (double.IsNaN(real) && double.IsNaN(imag))
        {
            if ((double.IsInfinity(ar) || double.IsInfinity(ai)) && double.IsFinite(br) && double.IsFinite(bi))
            {
                var x = double.CopySign(double.IsInfinity(ar) ? 1.0 : 0.0, ar);
                var y = double.CopySign(double.IsInfinity(ai) ? 1.0 : 0.0, ai);
                real = double.PositiveInfinity * (x * br + y * bi);
                imag = double.PositiveInfinity * (y * br - x * bi);
            }
            else if ((double.IsInfinity(absBr) || double.IsInfinity(absBi)) && double.IsFinite(ar) && double.IsFinite(ai))
            {
                var x = double.CopySign(double.IsInfinity(br) ? 1.0 : 0.0, br);
                var y = double.CopySign(double.IsInfinity(bi) ? 1.0 : 0.0, bi);
                real = 0.0 * (ar * x + ai * y);
                imag = 0.0 * (ai * x - ar * y);
            }
        }
        return new System.Numerics.Complex(real, imag);
    }

    // CPython _Py_cr_quot: a complex numerator over a real denominator is
    // component-wise; a zero denominator is the caller's EDOM case.
    private static System.Numerics.Complex ComplexCrQuot(System.Numerics.Complex a, double b)
    {
        return new(a.Real / b, a.Imaginary / b);
    }

    // CPython _Py_rc_quot: a real numerator over a complex denominator; the
    // negated imaginary term keeps CPython's signed-zero behavior.
    private static System.Numerics.Complex ComplexRcQuot(double a, System.Numerics.Complex b)
    {
        double br = b.Real, bi = b.Imaginary;
        double absBr = br < 0 ? -br : br;
        double absBi = bi < 0 ? -bi : bi;
        double real, imag;
        if (absBr >= absBi)
        {
            var ratio = bi / br;
            var denom = br + bi * ratio;
            real = a / denom;
            imag = (-a * ratio) / denom;
        }
        else if (absBi >= absBr)
        {
            var ratio = br / bi;
            var denom = br * ratio + bi;
            real = (a * ratio) / denom;
            imag = -a / denom;
        }
        else
        {
            real = imag = double.NaN;
        }
        if (double.IsNaN(real) && double.IsNaN(imag) && double.IsFinite(a) &&
            (double.IsInfinity(absBr) || double.IsInfinity(absBi)))
        {
            var x = double.CopySign(double.IsInfinity(br) ? 1.0 : 0.0, br);
            var y = double.CopySign(double.IsInfinity(bi) ? 1.0 : 0.0, bi);
            real = 0.0 * (a * x);
            imag = 0.0 * (-a * y);
        }
        return new System.Numerics.Complex(real, imag);
    }

    private static System.Numerics.Complex ComplexPow(System.Numerics.Complex baseValue, System.Numerics.Complex exponent)
    {
        // CPython _Py_c_pow: exp(b log a) in the exact operation order of
        // complexobject.c (the zero-exponent and zero-base cases never
        // reach here: complex_pow routes them elsewhere).
        var vabs = double.Hypot(baseValue.Real, baseValue.Imaginary);
        var len = double.Pow(vabs, exponent.Real);
        var at = double.Atan2(baseValue.Imaginary, baseValue.Real);
        var phase = at * exponent.Real;
        if (exponent.Imaginary is not 0.0)
        {
            len *= double.Exp(-at * exponent.Imaginary);
            phase += exponent.Imaginary * double.Log(vabs);
        }
        return new Complex(len * double.Cos(phase), len * double.Sin(phase));
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

        // CPython actual_complex_new: a single string argument goes through
        // the complex string parser, not the numeric conversion below.
        if (args[0] is PyStrObject str)
        {
            if (!TryParseComplexString(str.Value, out var parsed, out var underscoreError))
            {
                if (underscoreError)
                {
                    var repr = PySpecialMethods.Repr(context, str);
                    if (repr.IsError)
                        return repr;
                    return PyResult.ValueError(PySR.Runtime_Complex_CouldNotConvertString, repr.Value.Value);
                }
                return PyResult.ValueError(PySR.Runtime_Complex_MalformedString);
            }
            return CreateOfType(cls, parsed.Real, parsed.Imaginary);
        }

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
                if (!intObject.Value.TryToDoubleRounded(out var argReal))
                    return PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
                return PyComplexObject.FromRealImag(argReal, 0);
        }

        var index = PySpecialMethods.Index(context, arg);
        if (index.IsError)
            // complex() is one of the messages CPython renders with %T, the
            // fully qualified name (Objects/complexobject.c:1148)
            return PyResult.TypeError(errorMessage, arg.PyType.FullyQualifiedName);
        if (!index.Value.Value.TryToDoubleRounded(out var indexReal))
            return PyResult.OverflowError(PySR.Runtime_Number_IntTooLargeForFloat);
        return PyComplexObject.FromRealImag(indexReal, 0);
    }

    // CPython complex_subtype_from_string: a '_' pre-pass validates that
    // underscores sit between digits and strips them, then the inner
    // grammar accepts <float>, <float>j and <float><signed-float>j, plus
    // the legacy <float><sign>j, <sign>j and j forms, optionally wrapped
    // in the repr-style bracket.
    private static bool TryParseComplexString(string text, out Complex value, out bool underscoreError)
    {
        value = default;
        underscoreError = false;
        // the underscore pass validates ASCII digits, so the transform runs first
        string s = PyUnicodeData.TransformDecimalAndSpaceToAscii(text);
        if (s.Contains('_'))
        {
            var sb = new System.Text.StringBuilder(s.Length);
            var prev = '\0';
            foreach (var ch in s)
            {
                if (ch is '_')
                {
                    // underscores are only allowed after a digit
                    if (!(prev >= '0' && prev <= '9'))
                    {
                        underscoreError = true;
                        return false;
                    }
                }
                else
                {
                    sb.Append(ch);
                    // and only before a digit
                    if (prev is '_' && !(ch >= '0' && ch <= '9'))
                    {
                        underscoreError = true;
                        return false;
                    }
                }
                prev = ch;
            }
            // underscores are not allowed at the end
            if (prev is '_')
            {
                underscoreError = true;
                return false;
            }
            s = sb.ToString();
        }
        return TryParseComplexInner(s, out value);
    }

    private static bool TryParseComplexInner(string s, out Complex value)
    {
        value = default;
        double x = 0.0, y = 0.0;
        int i = 0;

        // position on first nonblank
        while (i < s.Length && PyFloatObjectType.IsPySpace(s[i]))
            i++;
        bool gotBracket = false;
        if (i < s.Length && s[i] is '(')
        {
            // skip over possible bracket from repr()
            gotBracket = true;
            i++;
            while (i < s.Length && PyFloatObjectType.IsPySpace(s[i]))
                i++;
        }

        if (TryParseFloatToken(s, ref i, out var z))
        {
            if (i < s.Length && s[i] is '+' or '-')
            {
                // <float><signed-float>j | <float><sign>j
                x = z;
                if (!TryParseFloatToken(s, ref i, out y))
                {
                    y = s[i] is '+' ? 1.0 : -1.0;
                    i++;
                }
                if (i >= s.Length || s[i] is not ('j' or 'J'))
                    return false;
                i++;
            }
            else if (i < s.Length && s[i] is 'j' or 'J')
            {
                // <float>j
                y = z;
                i++;
            }
            else
            {
                // <float>
                x = z;
            }
        }
        else
        {
            // not starting with <float>; must be <sign>j or j
            if (i < s.Length && s[i] is '+' or '-')
            {
                y = s[i] is '+' ? 1.0 : -1.0;
                i++;
            }
            else if (i < s.Length && s[i] is 'j' or 'J')
            {
                y = 1.0;
            }
            else
            {
                return false;
            }
            if (i >= s.Length || s[i] is not ('j' or 'J'))
                return false;
            i++;
        }

        // trailing whitespace and closing bracket
        while (i < s.Length && PyFloatObjectType.IsPySpace(s[i]))
            i++;
        if (gotBracket)
        {
            if (i >= s.Length || s[i] is not ')')
                return false;
            i++;
            while (i < s.Length && PyFloatObjectType.IsPySpace(s[i]))
                i++;
        }
        if (i != s.Length)
            return false;

        value = new Complex(x, y);
        return true;
    }

    // CPython _PyOS_ascii_strtod: the decimal grammar of _Py_dg_strtod
    // (optional sign, digits with optional point, optional exponent), and
    // when nothing decimal matched, an inf/infinity/nan token from
    // _Py_parse_inf_or_nan. An incomplete exponent leaves the position on
    // the 'e' instead of consuming it.
    private static bool TryParseFloatToken(string s, ref int i, out double value)
    {
        value = 0;
        int start = i;
        if (i < s.Length && s[i] is '+' or '-')
            i++;
        int digitsStart = i;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
            i++;
        bool anyDigit = i > digitsStart;
        if (i < s.Length && s[i] is '.')
        {
            i++;
            int fracStart = i;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9')
                i++;
            anyDigit |= i > fracStart;
        }
        if (!anyDigit)
        {
            int tokenEnd = PyFloatObjectType.ParseInfOrNan(s, start, out value);
            if (tokenEnd == start)
            {
                i = start;
                return false;
            }
            i = tokenEnd;
            return true;
        }
        if (i < s.Length && s[i] is 'e' or 'E')
        {
            int expMark = i;
            int j = i + 1;
            if (j < s.Length && s[j] is '+' or '-')
                j++;
            int expDigitsStart = j;
            while (j < s.Length && s[j] >= '0' && s[j] <= '9')
                j++;
            if (j > expDigitsStart)
                i = j;
            else
                i = expMark;
        }
        value = double.Parse(s[start..i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }
}

using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System.Numerics;

namespace PySharp.PyModules.Builtins;

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
}

public sealed class PyComplexObjectType : PyTypeObject<PyComplexObjectType, PyComplexObject>
{
    public override string Module => "builtins";
    public override string Name => "complex";

    protected override PyResult Repr(PyCallContext context, PyComplexObject self)
    {
        string s = $"({self.Value.Real}{(self.Value.Imaginary < 0 ? "-" : "+")}{Math.Abs(self.Value.Imaginary)}j)";
        return PyStrObject.FromString(s);
    }

    protected override PyResult Hash(PyCallContext context, PyComplexObject self)
    {
        int hash = HashCode.Combine(self.Value.Real, self.Value.Imaginary);
        return PyIntObject.FromInteger(hash);
    }

    protected override PyResult Bool(PyCallContext context, PyComplexObject self)
    {
        return PyBoolObject.FromBoolean(self.Value.Real != 0 || self.Value.Imaginary != 0);
    }

    protected override PyResult Int(PyCallContext context, PyComplexObject self)
    {
        if (self.Value.Imaginary != 0)
            return PyResult.TypeError(null);
        return PyIntObject.FromInteger((BigInteger)self.Value.Real);
    }

    protected override PyResult Float(PyCallContext context, PyComplexObject self)
    {
        if (self.Value.Imaginary != 0)
            return PyResult.TypeError(null);
        return PyFloatObject.FromDouble(self.Value.Real);
    }

    protected override PyResult Add(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
            return PyComplexObject.FromComplex(self.Value + c.Value);
        if (other is PyIntObject i)
            return PyComplexObject.FromComplex(self.Value + new Complex((double)i.Value, 0));
        if (other is PyFloatObject f)
            return PyComplexObject.FromComplex(self.Value + new Complex(f.Value, 0));
        return base.Add(context, self, other);
    }
    protected override PyResult Sub(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
            return PyComplexObject.FromComplex(self.Value - c.Value);
        if (other is PyIntObject i)
            return PyComplexObject.FromComplex(self.Value - new Complex((double)i.Value, 0));
        if (other is PyFloatObject f)
            return PyComplexObject.FromComplex(self.Value - new Complex(f.Value, 0));
        return base.Sub(context, self, other);
    }
    protected override PyResult Mul(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
            return PyComplexObject.FromComplex(self.Value * c.Value);
        if (other is PyIntObject i)
            return PyComplexObject.FromComplex(self.Value * new Complex((double)i.Value, 0));
        if (other is PyFloatObject f)
            return PyComplexObject.FromComplex(self.Value * new Complex(f.Value, 0));
        return base.Mul(context, self, other);
    }
    protected override PyResult TrueDiv(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
        {
            if (c.Value == System.Numerics.Complex.Zero)
                return PyResult.RaiseZeroDivisionError(null);
            return PyComplexObject.FromComplex(self.Value / c.Value);
        }
        if (other is PyIntObject i)
        {
            double v = (double)i.Value;
            if (v == 0)
                return PyResult.RaiseZeroDivisionError(null);
            return PyComplexObject.FromComplex(self.Value / new Complex(v, 0));
        }
        if (other is PyFloatObject f)
        {
            if (f.Value == 0)
                return PyResult.RaiseZeroDivisionError(null);
            return PyComplexObject.FromComplex(self.Value / new Complex(f.Value, 0));
        }
        return base.TrueDiv(context, self, other);
    }
    protected override PyResult Eq(PyCallContext context, PyComplexObject self, PyObject other)
    {
        if (other is PyComplexObject c)
            return PyBoolObject.FromBoolean(self.Value == c.Value);
        if (other is PyIntObject i)
            return PyBoolObject.FromBoolean(self.Value == new Complex((double)i.Value, 0));
        if (other is PyFloatObject f)
            return PyBoolObject.FromBoolean(self.Value == new Complex(f.Value, 0));
        return base.Eq(context, self, other);
    }
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        double real = 0, imag = 0;
        if (args.Count > 0)
        {
            if (args[0] is PyComplexObject c)
            {
                real = c.Real;
                imag = c.Imag;
            }
            else if (args[0] is PyFloatObject f)
                real = f.Value;
            else if (args[0] is PyIntObject i)
                real = (double)i.Value;
            else
            {
                var result = PySpecialMethods.Index(context, args[0]);
                if (result.IsError)
                    return result;

                real = (double)result.Value.Value;
            }
        }
        if (args.Count > 1)
        {
            if (args[1] is PyIntObject i)
                imag = (double)i.Value;
            else if (args[1] is PyFloatObject f)
                imag = f.Value;
            else
                return PyResult.TypeError(null);
        }
        var obj = PyComplexObject.FromRealImag(real, imag);
        obj._pyType = cls;
        return obj;
    }
}

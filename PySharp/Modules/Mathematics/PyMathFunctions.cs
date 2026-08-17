using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Numerics;

namespace PySharp.Modules.Mathematics;

internal static partial class PyMathFunctions
{
    [PyExport("sqrt", nameof(SqrtImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Sqrt { get; }
    [PyExport("acos", nameof(AcosImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Acos { get; }
    [PyExport("asin", nameof(AsinImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Asin { get; }
    [PyExport("atan", nameof(AtanImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Atan { get; }
    [PyExport("cos", nameof(CosImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Cos { get; }
    [PyExport("sin", nameof(SinImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Sin { get; }
    [PyExport("tan", nameof(TanImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Tan { get; }
    [PyExport("exp", nameof(ExpImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Exp { get; }
    [PyExport("acosh", nameof(AcoshImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Acosh { get; }
    [PyExport("asinh", nameof(AsinhImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Asinh { get; }
    [PyExport("atanh", nameof(AtanhImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Atanh { get; }
    [PyExport("cosh", nameof(CoshImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Cosh { get; }
    [PyExport("sinh", nameof(SinhImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Sinh { get; }
    [PyExport("tanh", nameof(TanhImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Tanh { get; }
    [PyExport("fabs", nameof(FabsImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Fabs { get; }
    [PyExport("ceil", nameof(CeilImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Ceil { get; }
    [PyExport("floor", nameof(FloorImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Floor { get; }
    [PyExport("trunc", nameof(TruncImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Trunc { get; }
    [PyExport("remainder", nameof(RemainderImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Remainder { get; }
    [PyExport("atan2", nameof(Atan2Impl))]
    public static partial PyBuiltinFunctionOrMethodObject Atan2 { get; }
    [PyExport("copysign", nameof(CopysignImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Copysign { get; }
    [PyExport("fmod", nameof(FmodImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Fmod { get; }
    [PyExport("pow", nameof(PowImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Pow { get; }
    [PyExport("gcd", nameof(GcdImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Gcd { get; }
    [PyExport("lcm", nameof(LcmImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Lcm { get; }
    [PyExport("log", nameof(LogImpl_1), nameof(LogImpl_2))]
    public static partial PyBuiltinFunctionOrMethodObject Log { get; }
    [PyExport("log2", nameof(Log2Impl))]
    public static partial PyBuiltinFunctionOrMethodObject Log2 { get; }
    [PyExport("log10", nameof(Log10Impl))]
    public static partial PyBuiltinFunctionOrMethodObject Log10 { get; }
    [PyExport("log1p", nameof(Log1pImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Log1p { get; }

    private static PyResult<PyFloatObject> Math1Impl(PyCallContext context, PyObject arg, Func<double, double> func, bool canOverflow, string? errMsg)
    {
        var xResult = PySpecialMethods.Float(context, arg);
        if (xResult.IsError)
            return xResult;

        var x = xResult.Value.Value;
        var r = func(x);

        if (double.IsNaN(r) && !double.IsNaN(x))
        {
            if (errMsg is not null)
                return PyResult.ValueError(errMsg, x);
            return PyResult.ValueError("math domain error");
        }

        if (double.IsInfinity(r) && double.IsFinite(x))
        {
            if (canOverflow)
                return PyResult.OverflowError("math range error");

            if (errMsg is not null)
                return PyResult.ValueError(errMsg, x);
            return PyResult.ValueError("math domain error");
        }

        return PyFloatObject.FromDouble(r);
    }

    private static PyResult<PyFloatObject> Math2Impl(PyCallContext context, PyObject arg0, PyObject arg1, Func<double, double, double> func)
    {
        var xResult = PySpecialMethods.Float(context, arg0);
        if (xResult.IsError)
            return xResult;
        var x = xResult.Value.Value;

        var yResult = PySpecialMethods.Float(context, arg1);
        if (yResult.IsError)
            return yResult;
        var y = yResult.Value.Value;

        var r = func(x, y);

        if (double.IsNaN(r))
        {
            if (!double.IsNaN(x) && !double.IsNaN(y))
                return PyResult.ValueError("math domain error");

            return PyFloatObject.FromDouble(r);
        }

        if (double.IsInfinity(r))
        {
            if (double.IsFinite(x) && double.IsFinite(y))
                return PyResult.OverflowError("math range error");
        }

        return PyFloatObject.FromDouble(r);
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult SqrtImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Sqrt, canOverflow: false, errMsg: "expected a nonnegative input, got {0}");
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult AcosImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Acos, canOverflow: false, errMsg: "expected a number in range from -1 up to 1, got {0}");
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult AsinImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Asin, canOverflow: false, errMsg: "expected a number in range from -1 up to 1, got {0}");
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult AtanImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Atan, canOverflow: false, errMsg: null);
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult CosImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Cos, canOverflow: false, errMsg: "expected a finite input, got {0}");
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult SinImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Sin, canOverflow: false, errMsg: "expected a finite input, got {0}");
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult TanImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Tan, canOverflow: false, errMsg: "expected a finite input, got {0}");
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult ExpImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Exp, canOverflow: true, errMsg: null);
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult AcoshImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Acosh, canOverflow: false, errMsg: "expected argument value not less than 1, got {0}");
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult AsinhImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Asinh, canOverflow: false, errMsg: null);
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult AtanhImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Atanh, canOverflow: false, errMsg: "expected a number between -1 and 1, got {0}");
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult CoshImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Cosh, canOverflow: true, errMsg: null);
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult SinhImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Sinh, canOverflow: true, errMsg: null);
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult TanhImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Tanh, canOverflow: false, errMsg: null);
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult FabsImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Math.Abs, canOverflow: false, errMsg: null);
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult CeilImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Ceil(context, arguments[0]);
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult FloorImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Floor(context, arguments[0]);
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult TruncImpl(PyCallContext context, PyArguments arguments)
    {
        return PySpecialMethods.Trunc(context, arguments[0]);
    }

    [PyFunctionParameters("x", "y", "/")]
    private static PyResult RemainderImpl(PyCallContext context, PyArguments arguments)
    {
        return Math2Impl(context, arguments[0], arguments[1], Math.IEEERemainder);
    }

    [PyFunctionParameters("y", "x", "/")]
    private static PyResult Atan2Impl(PyCallContext context, PyArguments arguments)
    {
        return Math2Impl(context, arguments[0], arguments[1], Math.Atan2);
    }

    [PyFunctionParameters("x", "y", "/")]
    private static PyResult CopysignImpl(PyCallContext context, PyArguments arguments)
    {
        return Math2Impl(context, arguments[0], arguments[1], Math.CopySign);
    }

    [PyFunctionParameters("x", "y", "/")]
    private static PyResult FmodImpl(PyCallContext context, PyArguments arguments)
    {
        return Math2Impl(context, arguments[0], arguments[1], static (x, y) => x % y);
    }

    [PyFunctionParameters("x", "y", "/")]
    private static PyResult PowImpl(PyCallContext context, PyArguments arguments)
    {
        var xResult = PySpecialMethods.Float(context, arguments[0]);
        if (xResult.IsError)
            return xResult;
        var x = xResult.Value.Value;

        var yResult = PySpecialMethods.Float(context, arguments[1]);
        if (yResult.IsError)
            return yResult;
        var y = yResult.Value.Value;

        if (y is 0.0)
            return PyFloatObject.One;

        if (x is 1.0)
            return PyFloatObject.One;

        var r = Math.Pow(x, y);


        if (!double.IsFinite(r))
        {
            if (double.IsNaN(r))
                return PyResult.ValueError("math domain error");

            if (double.IsInfinity(r) && double.IsFinite(x) && double.IsFinite(y))
            {
                if (x is 0.0)
                    return PyResult.ValueError("math domain error");
                else
                    return PyResult.OverflowError("math range error");
            }
        }

        return PyFloatObject.FromDouble(r);
    }

    [PyFunctionParameters("*integers")]
    private static PyResult GcdImpl(PyCallContext context, PyArguments arguments)
    {
        var result = BigInteger.Zero;

        foreach (var arg in arguments.ExtraArgs)
        {
            var intResult = PySpecialMethods.Index(context, arg);
            if (intResult.IsError)
                return intResult;

            result = BigInteger.GreatestCommonDivisor(result, intResult.Value.Value);
        }

        return PyIntObject.FromInteger(result);
    }

    [PyFunctionParameters("*integers")]
    private static PyResult LcmImpl(PyCallContext context, PyArguments arguments)
    {
        var result = BigInteger.One;

        foreach (var arg in arguments.ExtraArgs)
        {
            var intResult = PySpecialMethods.Index(context, arg);
            if (intResult.IsError)
                return intResult;

            result = LeastCommonMultiple(result, intResult.Value.Value);
        }

        return PyIntObject.FromInteger(result);

        static BigInteger LeastCommonMultiple(BigInteger left, BigInteger right)
        {
            if (left.IsZero || right.IsZero)
                return BigInteger.Zero;

            if (left == right)
                return BigInteger.Abs(left);

            return BigInteger.Abs(left / BigInteger.GreatestCommonDivisor(left, right) * right);
        }
    }

    private static PyResult<PyFloatObject> MathLogImpl(PyCallContext context, PyObject arg, Func<BigInteger, double> intFunc, Func<double, double> doubleFunc)
    {
        if (arg is PyIntObject { Value: var intValue })
        {
            if (intValue <= 0)
                return PyResult.ValueError("math domain error");
            return PyFloatObject.FromDouble(intFunc(intValue));
        }

        if (arg.PyType.Slots.Float is not null)
        {
            var result = PySpecialMethods.Float(context, arg);
            if (result.IsError)
                return result;

            return Math1Impl(context, result.Value, doubleFunc, canOverflow: false, errMsg: "expected a positive input, got {0}");
        }
        else
        {
            var result = PySpecialMethods.Index(context, arg);
            if (result.IsError)
                return result.ExceptionResult;

            if (result.Value.Value <= 0)
                return PyResult.ValueError("math domain error");
            return PyFloatObject.FromDouble(intFunc(result.Value.Value));
        }
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult LogImpl_1(PyCallContext context, PyArguments arguments)
    {
        return MathLogImpl(context, arguments[0], BigInteger.Log, Math.Log);
    }
    [PyFunctionParameters("x", "base", "/")]
    private static PyResult LogImpl_2(PyCallContext context, PyArguments arguments)
    {
        var num = MathLogImpl(context, arguments[0], BigInteger.Log, Math.Log);
        if (num.IsError)
            return num;

        var den = MathLogImpl(context, arguments[1], BigInteger.Log, Math.Log);
        if (den.IsError)
            return den;

        return PyFloatObject.FromDouble(num.Value.Value / den.Value.Value);
    }

    [PyFunctionParameters("x", "/")]
    private static PyResult Log2Impl(PyCallContext context, PyArguments arguments)
    {
        return MathLogImpl(context, arguments[0], static i => (double)BigInteger.Log(i, 2), Math.Log2);
    }
    [PyFunctionParameters("x", "/")]
    private static PyResult Log10Impl(PyCallContext context, PyArguments arguments)
    {
        return MathLogImpl(context, arguments[0], BigInteger.Log10, Math.Log10);
    }
    [PyFunctionParameters("x", "/")]
    private static PyResult Log1pImpl(PyCallContext context, PyArguments arguments)
    {
        return Math1Impl(context, arguments[0], Log1p, canOverflow: false, errMsg: "expected argument value > -1, got {0}");

        static double Log1p(double value)
        {
            if (double.IsNaN(value) || value < -1.0)
                return double.NaN;

            if (value is -1.0)
                return double.NegativeInfinity;

            if (double.IsPositiveInfinity(value))
                return double.PositiveInfinity;

            double u = 1.0 + value;
            double epsilon = u - 1.0;

            if (epsilon is 0)
                return value;

            return Math.Log(u) * (value / epsilon);
        }
    }
}

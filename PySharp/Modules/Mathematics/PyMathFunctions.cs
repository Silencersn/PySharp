using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace PySharp.Modules.Mathematics;

internal static class PyMathFunctions
{
    public static readonly PyBuiltinFunctionOrMethodObject Sqrt = PyBuiltinFunctionOrMethodObject.CreateFunction("sqrt", SqrtImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Acos = PyBuiltinFunctionOrMethodObject.CreateFunction("acos", AcosImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Asin = PyBuiltinFunctionOrMethodObject.CreateFunction("asin", AsinImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Atan = PyBuiltinFunctionOrMethodObject.CreateFunction("atan", AtanImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Cos = PyBuiltinFunctionOrMethodObject.CreateFunction("cos", CosImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Sin = PyBuiltinFunctionOrMethodObject.CreateFunction("sin", SinImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Tan = PyBuiltinFunctionOrMethodObject.CreateFunction("tan", TanImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Exp = PyBuiltinFunctionOrMethodObject.CreateFunction("exp", ExpImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Acosh = PyBuiltinFunctionOrMethodObject.CreateFunction("acosh", AcoshImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Asinh = PyBuiltinFunctionOrMethodObject.CreateFunction("asinh", AsinhImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Atanh = PyBuiltinFunctionOrMethodObject.CreateFunction("atanh", AtanhImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Cosh = PyBuiltinFunctionOrMethodObject.CreateFunction("cosh", CoshImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Sinh = PyBuiltinFunctionOrMethodObject.CreateFunction("sinh", SinhImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Tanh = PyBuiltinFunctionOrMethodObject.CreateFunction("tanh", TanhImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Fabs = PyBuiltinFunctionOrMethodObject.CreateFunction("fabs", FabsImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Ceil = PyBuiltinFunctionOrMethodObject.CreateFunction("ceil", CeilImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Floor = PyBuiltinFunctionOrMethodObject.CreateFunction("floor", FloorImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Trunc = PyBuiltinFunctionOrMethodObject.CreateFunction("trunc", TruncImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Remainder = PyBuiltinFunctionOrMethodObject.CreateFunction("remainder", RemainderImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Atan2 = PyBuiltinFunctionOrMethodObject.CreateFunction("atan2", Atan2Impl);
    public static readonly PyBuiltinFunctionOrMethodObject Copysign = PyBuiltinFunctionOrMethodObject.CreateFunction("copysign", CopysignImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Fmod = PyBuiltinFunctionOrMethodObject.CreateFunction("fmod", FmodImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Pow = PyBuiltinFunctionOrMethodObject.CreateFunction("pow", PowImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Gcd = PyBuiltinFunctionOrMethodObject.CreateFunction("gcd", GcdImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Lcm = PyBuiltinFunctionOrMethodObject.CreateFunction("lcm", LcmImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Log = PyBuiltinFunctionOrMethodObject.CreateFunction("log", LogImpl_1, LogImpl_2);
    public static readonly PyBuiltinFunctionOrMethodObject Log2 = PyBuiltinFunctionOrMethodObject.CreateFunction("log2", Log2Impl);
    public static readonly PyBuiltinFunctionOrMethodObject Log10 = PyBuiltinFunctionOrMethodObject.CreateFunction("log10", Log10Impl);
    public static readonly PyBuiltinFunctionOrMethodObject Log1p = PyBuiltinFunctionOrMethodObject.CreateFunction("log1p", Log1pImpl);

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
            return PyFloatObject.FromDouble(intFunc(intValue));

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

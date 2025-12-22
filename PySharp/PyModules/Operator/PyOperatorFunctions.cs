using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Operator;

public static class PyOperatorFunctions
{
    public static readonly PyBuiltinFunctionOrMethodObject Add = new("add", AddImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Sub = new("sub", SubImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Mul = new("mul", MulImpl);
    public static readonly PyBuiltinFunctionOrMethodObject TrueDiv = new("truediv", TrueDivImpl);
    public static readonly PyBuiltinFunctionOrMethodObject FloorDiv = new("floordiv", FloorDivImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Mod = new("mod", ModImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Pow = new("pow", PowImpl);
    public static readonly PyBuiltinFunctionOrMethodObject LShift = new("lshift", LShiftImpl);
    public static readonly PyBuiltinFunctionOrMethodObject RShift = new("rshift", RShiftImpl);
    public static readonly PyBuiltinFunctionOrMethodObject And = new("and_", AndImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Xor = new("xor", XorImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Or = new("or_", OrImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Lt = new("lt", LtImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Le = new("le", LeImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Eq = new("eq", EqImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Ne = new("ne", NeImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Gt = new("gt", GtImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Ge = new("ge", GeImpl);

    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult AddImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Add(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult SubImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Sub(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult MulImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Mul(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult TrueDivImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.TrueDiv(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult FloorDivImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.FloorDiv(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult ModImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Mod(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult PowImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Pow(arguments.Args[0], arguments.Args[1], PyNoneObject.None);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult LShiftImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.LShift(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult RShiftImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.RShift(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult AndImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.And(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult XorImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Xor(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult OrImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Or(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult LtImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Lt(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult LeImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Le(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult EqImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Eq(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult NeImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Ne(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult GtImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Gt(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult GeImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PyOperators.Ge(arguments.Args[0], arguments.Args[1]);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
}
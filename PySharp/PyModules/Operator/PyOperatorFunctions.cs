using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Operator;

public static class PyOperatorFunctions
{
    public static readonly PyBuiltinFunctionOrMethodObject Add = PyBuiltinFunctionOrMethodObject.CreateFunction("add", AddImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Sub = PyBuiltinFunctionOrMethodObject.CreateFunction("sub", SubImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Mul = PyBuiltinFunctionOrMethodObject.CreateFunction("mul", MulImpl);
    public static readonly PyBuiltinFunctionOrMethodObject TrueDiv = PyBuiltinFunctionOrMethodObject.CreateFunction("truediv", TrueDivImpl);
    public static readonly PyBuiltinFunctionOrMethodObject FloorDiv = PyBuiltinFunctionOrMethodObject.CreateFunction("floordiv", FloorDivImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Mod = PyBuiltinFunctionOrMethodObject.CreateFunction("mod", ModImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Pow = PyBuiltinFunctionOrMethodObject.CreateFunction("pow", PowImpl);
    public static readonly PyBuiltinFunctionOrMethodObject LShift = PyBuiltinFunctionOrMethodObject.CreateFunction("lshift", LShiftImpl);
    public static readonly PyBuiltinFunctionOrMethodObject RShift = PyBuiltinFunctionOrMethodObject.CreateFunction("rshift", RShiftImpl);
    public static readonly PyBuiltinFunctionOrMethodObject And = PyBuiltinFunctionOrMethodObject.CreateFunction("and_", AndImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Xor = PyBuiltinFunctionOrMethodObject.CreateFunction("xor", XorImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Or = PyBuiltinFunctionOrMethodObject.CreateFunction("or_", OrImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Lt = PyBuiltinFunctionOrMethodObject.CreateFunction("lt", LtImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Le = PyBuiltinFunctionOrMethodObject.CreateFunction("le", LeImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Eq = PyBuiltinFunctionOrMethodObject.CreateFunction("eq", EqImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Ne = PyBuiltinFunctionOrMethodObject.CreateFunction("ne", NeImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Gt = PyBuiltinFunctionOrMethodObject.CreateFunction("gt", GtImpl);
    public static readonly PyBuiltinFunctionOrMethodObject Ge = PyBuiltinFunctionOrMethodObject.CreateFunction("ge", GeImpl);

    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult AddImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Add(context, arguments.Args[0], arguments.Args[1]);
    }

    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult SubImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Sub(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult MulImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Mult(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult TrueDivImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.TrueDiv(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult FloorDivImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.FloorDiv(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult ModImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Mod(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult PowImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Pow(context, arguments.Args[0], arguments.Args[1], PyNoneObject.None);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult LShiftImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.LShift(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult RShiftImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.RShift(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult AndImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.BitAnd(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult XorImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.BitXor(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult OrImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.BitOr(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult LtImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Lt(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult LeImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.LtE(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult EqImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Eq(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult NeImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.NotEq(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult GtImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Gt(context, arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyResult GeImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.GtE(context, arguments.Args[0], arguments.Args[1]);
    }
}
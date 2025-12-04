using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
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
    private static PyObject? AddImpl(PyArguments arguments)
    {
        return PyOperators.Add(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? SubImpl(PyArguments arguments)
    {
        return PyOperators.Sub(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? MulImpl(PyArguments arguments)
    {
        return PyOperators.Mul(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? TrueDivImpl(PyArguments arguments)
    {
        return PyOperators.TrueDiv(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? FloorDivImpl(PyArguments arguments)
    {
        return PyOperators.FloorDiv(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? ModImpl(PyArguments arguments)
    {
        return PyOperators.Mod(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? PowImpl(PyArguments arguments)
    {
        return PyOperators.Pow(arguments.Args[0], arguments.Args[1], PyNoneObject.None);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? LShiftImpl(PyArguments arguments)
    {
        return PyOperators.LShift(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? RShiftImpl(PyArguments arguments)
    {
        return PyOperators.RShift(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? AndImpl(PyArguments arguments)
    {
        return PyOperators.And(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? XorImpl(PyArguments arguments)
    {
        return PyOperators.Xor(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? OrImpl(PyArguments arguments)
    {
        return PyOperators.Or(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? LtImpl(PyArguments arguments)
    {
        return PyOperators.Lt(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? LeImpl(PyArguments arguments)
    {
        return PyOperators.Le(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? EqImpl(PyArguments arguments)
    {
        return PyOperators.Eq(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? NeImpl(PyArguments arguments)
    {
        return PyOperators.Ne(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? GtImpl(PyArguments arguments)
    {
        return PyOperators.Gt(arguments.Args[0], arguments.Args[1]);
    }
    [PyFunctionArgsDef("a", "b", "/")]
    private static PyObject? GeImpl(PyArguments arguments)
    {
        return PyOperators.Ge(arguments.Args[0], arguments.Args[1]);
    }
}
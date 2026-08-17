using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Operator;

public static partial class PyOperatorFunctions
{
    [PyExport("add", nameof(AddImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Add { get; }
    [PyExport("sub", nameof(SubImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Sub { get; }
    [PyExport("mul", nameof(MulImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Mul { get; }
    [PyExport("truediv", nameof(TrueDivImpl))]
    public static partial PyBuiltinFunctionOrMethodObject TrueDiv { get; }
    [PyExport("floordiv", nameof(FloorDivImpl))]
    public static partial PyBuiltinFunctionOrMethodObject FloorDiv { get; }
    [PyExport("mod", nameof(ModImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Mod { get; }
    [PyExport("pow", nameof(PowImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Pow { get; }
    [PyExport("lshift", nameof(LShiftImpl))]
    public static partial PyBuiltinFunctionOrMethodObject LShift { get; }
    [PyExport("rshift", nameof(RShiftImpl))]
    public static partial PyBuiltinFunctionOrMethodObject RShift { get; }
    [PyExport("and_", nameof(AndImpl))]
    public static partial PyBuiltinFunctionOrMethodObject And { get; }
    [PyExport("xor", nameof(XorImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Xor { get; }
    [PyExport("or_", nameof(OrImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Or { get; }
    [PyExport("lt", nameof(LtImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Lt { get; }
    [PyExport("le", nameof(LeImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Le { get; }
    [PyExport("eq", nameof(EqImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Eq { get; }
    [PyExport("ne", nameof(NeImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Ne { get; }
    [PyExport("gt", nameof(GtImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Gt { get; }
    [PyExport("ge", nameof(GeImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Ge { get; }

    [PyFunctionParameters("a", "b", "/")]
    private static PyResult AddImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Add(context, arguments[0], arguments[1]);
    }

    [PyFunctionParameters("a", "b", "/")]
    private static PyResult SubImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Sub(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult MulImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Mult(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult TrueDivImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.TrueDiv(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult FloorDivImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.FloorDiv(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult ModImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Mod(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult PowImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Pow(context, arguments[0], arguments[1], PyNoneObject.None);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult LShiftImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.LShift(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult RShiftImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.RShift(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult AndImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.BitAnd(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult XorImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.BitXor(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult OrImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.BitOr(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult LtImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Lt(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult LeImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.LtE(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult EqImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Eq(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult NeImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.NotEq(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult GtImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.Gt(context, arguments[0], arguments[1]);
    }
    [PyFunctionParameters("a", "b", "/")]
    private static PyResult GeImpl(PyCallContext context, PyArguments arguments)
    {
        return PyOperators.GtE(context, arguments[0], arguments[1]);
    }
}

using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.AstNodes;

public abstract class AstUnaryOpNode : AstNode
{
    public abstract PyResult GetUnaryOpValue(PyCallContext context, PyObject value);
}

public class NotNode : AstUnaryOpNode
{
    public static NotNode Shared { get; } = new();

    public override PyResult GetUnaryOpValue(PyCallContext context, PyObject value)
    {
        if (!PySpecialMethods.TryGetBool(value, out var b))
            return PyResult.CaptureExceptionFromPVM();
        return PyBoolObject.FromBoolean(!b.BoolValue);
    }
}

public class InvertNode : AstUnaryOpNode
{
    public static InvertNode Shared { get; } = new();

    public override PyResult GetUnaryOpValue(PyCallContext context, PyObject value)
    {
        return value.Invert(context);
    }
}

public class UAddNode : AstUnaryOpNode
{
    public static UAddNode Shared { get; } = new();

    public override PyResult GetUnaryOpValue(PyCallContext context, PyObject value)
    {
        return value.Pos(context);
    }
}

public class USubNode : AstUnaryOpNode
{
    public static USubNode Shared { get; } = new();

    public override PyResult GetUnaryOpValue(PyCallContext context, PyObject value)
    {
        return value.Neg(context);
    }
}

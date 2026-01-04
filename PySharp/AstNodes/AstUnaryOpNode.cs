using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.AstNodes;

public abstract class AstUnaryOpNode : AstNode
{
    public abstract PyResult GetUnaryOpValue(PyCallContext context, PyObject value);

    public sealed override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public class NotNode : AstUnaryOpNode
{
    internal static NotNode Shared { get; } = new();
    private NotNode() { }

    public override PyResult GetUnaryOpValue(PyCallContext context, PyObject value)
    {
        if (!PySpecialMethods.TryGetBool(context, value, out var b, out var result))
            return result;
        return PyBoolObject.FromBoolean(!b.BoolValue);
    }
}

public class InvertNode : AstUnaryOpNode
{
    internal static InvertNode Shared { get; } = new();
    private InvertNode() { }

    public override PyResult GetUnaryOpValue(PyCallContext context, PyObject value)
    {
        return value.Invert(context);
    }
}

public class UAddNode : AstUnaryOpNode
{
    internal static UAddNode Shared { get; } = new();
    private UAddNode() { }

    public override PyResult GetUnaryOpValue(PyCallContext context, PyObject value)
    {
        return value.Pos(context);
    }
}

public class USubNode : AstUnaryOpNode
{
    internal static USubNode Shared { get; } = new();
    private USubNode() { }

    public override PyResult GetUnaryOpValue(PyCallContext context, PyObject value)
    {
        return value.Neg(context);
    }
}

using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;

namespace PySharp.AstNodes;

public abstract class AstUnaryOpNode : AstNode
{
    public abstract PyObject? GetUnaryOpValue(PyObject value);
}

public class NotNode : AstUnaryOpNode
{
    public static NotNode Shared { get; } = new();

    public override PyBoolObject? GetUnaryOpValue(PyObject value)
    {
        if (!PySpecialMethods.TryGetBool(value, out var b))
            return null;
        return PyBoolObject.FromBoolean(!b.BoolValue);
    }
}

public class InvertNode : AstUnaryOpNode
{
    public static InvertNode Shared { get; } = new();

    public override PyObject? GetUnaryOpValue(PyObject value)
    {
        return value.Invert();
    }
}

public class UAddNode : AstUnaryOpNode
{
    public static UAddNode Shared { get; } = new();

    public override PyObject? GetUnaryOpValue(PyObject value)
    {
        return value.Pos();
    }
}

public class USubNode : AstUnaryOpNode
{
    public static USubNode Shared { get; } = new();

    public override PyObject? GetUnaryOpValue(PyObject value)
    {
        return value.Neg();
    }
}

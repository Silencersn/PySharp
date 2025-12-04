using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;

namespace PySharp.AstNodes;

public abstract class AstOperatorNode : AstNode
{
    public abstract PyObject? GetOpValue(PyObject left, PyObject right);
}

public class AddNode : AstOperatorNode
{
    public static AddNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.Add(left, right);
    }
}

public class SubNode : AstOperatorNode
{
    public static SubNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.Sub(left, right);
    }
}

public class MulNode : AstOperatorNode
{
    public static MulNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.Mul(left, right);
    }
}

public class DivNode : AstOperatorNode
{
    public static DivNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.TrueDiv(left, right);
    }
}

public class FloorDivNode : AstOperatorNode
{
    public static FloorDivNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.FloorDiv(left, right);
    }
}

public class ModNode : AstOperatorNode
{
    public static ModNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.Mod(left, right);
    }
}

public class PowNode : AstOperatorNode
{
    public static PowNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.Pow(left, right, PyNoneObject.None);
    }
}

public class LShiftNode : AstOperatorNode
{
    public static LShiftNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.LShift(left, right);
    }
}

public class RShiftNode : AstOperatorNode
{
    public static RShiftNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.RShift(left, right);
    }
}

public class BitOrNode : AstOperatorNode
{
    public static BitOrNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.Or(left, right);
    }
}

public class BitXorNode : AstOperatorNode
{
    public static BitXorNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.Xor(left, right);
    }
}

public class BitAndNode : AstOperatorNode
{
    public static BitAndNode Shared { get; } = new();

    public override PyObject? GetOpValue(PyObject left, PyObject right)
    {
        return PyOperators.And(left, right);
    }
}
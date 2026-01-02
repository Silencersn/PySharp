using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.AstNodes;

public abstract class AstOperatorNode : AstNode
{
    public abstract PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right);

    public sealed override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public class AddNode : AstOperatorNode
{
    public static AddNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Add(context, left, right);
    }
}

public class SubNode : AstOperatorNode
{
    public static SubNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Sub(context, left, right);
    }
}

public class MulNode : AstOperatorNode
{
    public static MulNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Mul(context, left, right);
    }
}

public class DivNode : AstOperatorNode
{
    public static DivNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.TrueDiv(context, left, right);
    }
}

public class FloorDivNode : AstOperatorNode
{
    public static FloorDivNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.FloorDiv(context, left, right);
    }
}

public class ModNode : AstOperatorNode
{
    public static ModNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Mod(context, left, right);
    }
}

public class PowNode : AstOperatorNode
{
    public static PowNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Pow(context, left, right, PyNoneObject.None);
    }
}

public class LShiftNode : AstOperatorNode
{
    public static LShiftNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.LShift(context, left, right);
    }
}

public class RShiftNode : AstOperatorNode
{
    public static RShiftNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.RShift(context, left, right);
    }
}

public class BitOrNode : AstOperatorNode
{
    public static BitOrNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Or(context, left, right);
    }
}

public class BitXorNode : AstOperatorNode
{
    public static BitXorNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Xor(context, left, right);
    }
}

public class BitAndNode : AstOperatorNode
{
    public static BitAndNode Shared { get; } = new();

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.And(context, left, right);
    }
}
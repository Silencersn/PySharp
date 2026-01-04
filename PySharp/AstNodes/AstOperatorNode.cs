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
    internal static AddNode Shared { get; } = new();
    private AddNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Add(context, left, right);
    }
}

public class SubNode : AstOperatorNode
{
    internal static SubNode Shared { get; } = new();
    private SubNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Sub(context, left, right);
    }
}

public class MulNode : AstOperatorNode
{
    internal static MulNode Shared { get; } = new();
    private MulNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Mul(context, left, right);
    }
}

public class DivNode : AstOperatorNode
{
    internal static DivNode Shared { get; } = new();
    private DivNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.TrueDiv(context, left, right);
    }
}

public class FloorDivNode : AstOperatorNode
{
    internal static FloorDivNode Shared { get; } = new();
    private FloorDivNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.FloorDiv(context, left, right);
    }
}

public class ModNode : AstOperatorNode
{
    internal static ModNode Shared { get; } = new();
    private ModNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Mod(context, left, right);
    }
}

public class PowNode : AstOperatorNode
{
    internal static PowNode Shared { get; } = new();
    private PowNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Pow(context, left, right, PyNoneObject.None);
    }
}

public class LShiftNode : AstOperatorNode
{
    internal static LShiftNode Shared { get; } = new();
    private LShiftNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.LShift(context, left, right);
    }
}

public class RShiftNode : AstOperatorNode
{
    internal static RShiftNode Shared { get; } = new();
    private RShiftNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.RShift(context, left, right);
    }
}

public class BitOrNode : AstOperatorNode
{
    internal static BitOrNode Shared { get; } = new();
    private BitOrNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Or(context, left, right);
    }
}

public class BitXorNode : AstOperatorNode
{
    internal static BitXorNode Shared { get; } = new();
    private BitXorNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Xor(context, left, right);
    }
}

public class BitAndNode : AstOperatorNode
{
    internal static BitAndNode Shared { get; } = new();
    private BitAndNode() { }

    public override PyResult GetOpValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.And(context, left, right);
    }
}
using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.AstNodes;

public abstract class AstCmpopNode : AstNode
{
    public abstract PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right);

    public sealed override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public class EqNode : AstCmpopNode
{
    internal static EqNode Shared { get; } = new();
    private EqNode() { }

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Eq(context, left, right);
    }
}

public class NotEqNode : AstCmpopNode
{
    internal static NotEqNode Shared { get; } = new();
    private NotEqNode() { }

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Ne(context, left, right);
    }
}

public class LtNode : AstCmpopNode
{
    internal static LtNode Shared { get; } = new();
    private LtNode() { }

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Lt(context, left, right);
    }
}

public class LtENode : AstCmpopNode
{
    internal static LtENode Shared { get; } = new();
    private LtENode() { }

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Le(context, left, right);
    }
}

public class GtNode : AstCmpopNode
{
    internal static GtNode Shared { get; } = new();
    private GtNode() { }

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Gt(context, left, right);
    }
}

public class GtENode : AstCmpopNode
{
    internal static GtENode Shared { get; } = new();
    private GtENode() { }

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Ge(context, left, right);
    }
}

public class IsNode : AstCmpopNode
{
    internal static IsNode Shared { get; } = new();
    private IsNode() { }

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Is(left, right);
    }
}

public class IsNotNode : AstCmpopNode
{
    internal static IsNotNode Shared { get; } = new();
    private IsNotNode() { }

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.IsNot(left, right);
    }
}

public class InNode : AstCmpopNode
{
    internal static InNode Shared { get; } = new();
    private InNode() { }

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return right.Contains(context, left);
    }
}

public class NotInNode : AstCmpopNode
{
    internal static NotInNode Shared { get; } = new();
    private NotInNode() { }

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        var contains = right.Contains(context, left);
        if (contains.IsError)
            return contains;

        if (!PySpecialMethods.TryGetBool(context, contains.Value, out var b, out var result))
            return result;

        return PyBoolObject.FromBoolean(!b.BoolValue);
    }
}
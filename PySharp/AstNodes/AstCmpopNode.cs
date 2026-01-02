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
    public static EqNode Shared { get; } = new();

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Eq(context, left, right);
    }
}

public class NotEqNode : AstCmpopNode
{
    public static NotEqNode Shared { get; } = new();

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Ne(context, left, right);
    }
}

public class LtNode : AstCmpopNode
{
    public static LtNode Shared { get; } = new();

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Lt(context, left, right);
    }
}

public class LtENode : AstCmpopNode
{
    public static LtENode Shared { get; } = new();

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Le(context, left, right);
    }
}

public class GtNode : AstCmpopNode
{
    public static GtNode Shared { get; } = new();

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Gt(context, left, right);
    }
}

public class GtENode : AstCmpopNode
{
    public static GtENode Shared { get; } = new();

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Ge(context, left, right);
    }
}

public class IsNode : AstCmpopNode
{
    public static IsNode Shared { get; } = new();

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.Is(left, right);
    }
}

public class IsNotNode : AstCmpopNode
{
    public static IsNotNode Shared { get; } = new();

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return PyOperators.IsNot(left, right);
    }
}

public class InNode : AstCmpopNode
{
    public static InNode Shared { get; } = new();

    public override PyResult GetCompareValue(PyCallContext context, PyObject left, PyObject right)
    {
        return right.Contains(context, left);
    }
}

public class NotInNode : AstCmpopNode
{
    public static NotInNode Shared { get; } = new();

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
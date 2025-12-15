using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;

namespace PySharp.AstNodes;

public abstract class AstCmpopNode : AstNode
{
    public abstract PyObject? GetCompareValue(PyObject left, PyObject right);
}

public class EqNode : AstCmpopNode
{
    public static EqNode Shared { get; } = new();

    public override PyObject? GetCompareValue(PyObject left, PyObject right)
    {
        var eq = PyOperators.Eq(left, right);
        if (eq is PyNotImplementedObject)
            return PyOperators.Is(left, right);
        return eq;
    }
}

public class NotEqNode : AstCmpopNode
{
    public static NotEqNode Shared { get; } = new();

    public override PyObject? GetCompareValue(PyObject left, PyObject right)
    {
        var ne = PyOperators.Ne(left, right);
        if (ne is PyNotImplementedObject)
            return PyOperators.IsNot(left, right);
        return ne;
    }
}

public class LtNode : AstCmpopNode
{
    public static LtNode Shared { get; } = new();

    public override PyObject? GetCompareValue(PyObject left, PyObject right)
    {
        return PyOperators.Lt(left, right);
    }
}

public class LtENode : AstCmpopNode
{
    public static LtENode Shared { get; } = new();

    public override PyObject? GetCompareValue(PyObject left, PyObject right)
    {
        return PyOperators.Le(left, right);
    }
}

public class GtNode : AstCmpopNode
{
    public static GtNode Shared { get; } = new();

    public override PyObject? GetCompareValue(PyObject left, PyObject right)
    {
        return PyOperators.Gt(left, right);
    }
}

public class GtENode : AstCmpopNode
{
    public static GtENode Shared { get; } = new();

    public override PyObject? GetCompareValue(PyObject left, PyObject right)
    {
        return PyOperators.Ge(left, right);
    }
}

public class IsNode : AstCmpopNode
{
    public static IsNode Shared { get; } = new();

    public override PyObject? GetCompareValue(PyObject left, PyObject right)
    {
        return PyOperators.Is(left, right);
    }
}

public class IsNotNode : AstCmpopNode
{
    public static IsNotNode Shared { get; } = new();

    public override PyObject? GetCompareValue(PyObject left, PyObject right)
    {
        return PyOperators.IsNot(left, right);
    }
}

public class InNode : AstCmpopNode
{
    public static InNode Shared { get; } = new();

    public override PyObject? GetCompareValue(PyObject left, PyObject right)
    {
        var contains = right.Contains(left);
        if (contains is null)
            return null;
        return PySpecialMethods.GetBool(contains);
    }
}

public class NotInNode : AstCmpopNode
{
    public static NotInNode Shared { get; } = new();

    public override PyObject? GetCompareValue(PyObject left, PyObject right)
    {
        var contains = right.Contains(left);
        if (contains is null)
            return null;

        if (!PySpecialMethods.TryGetBool(contains, out var b))
            return null;

        return PyBoolObject.FromBoolean(!b.BoolValue);
    }
}
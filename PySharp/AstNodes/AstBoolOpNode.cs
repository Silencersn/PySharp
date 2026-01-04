using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.AstNodes;

public abstract class AstBoolOpNode : AstNode
{
    public abstract (bool Result, PyResult Value) GetBoolOpValue(PyCallContext context, IEnumerable<PyObject> values);

    public sealed override IEnumerable<AstNode> EnumerateSubNodes()
    {
        return [];
    }
}

public class AndNode : AstBoolOpNode
{
    internal static AndNode Shared { get; } = new();
    private AndNode() { }

    public override (bool Result, PyResult Value) GetBoolOpValue(PyCallContext context, IEnumerable<PyObject> values)
    {
        PyObject lastValue = null!;

        foreach (var value in values)
        {
            if (!PySpecialMethods.TryGetBool(context, value, out var b, out var result))
                return (false, result);

            if (!b.BoolValue)
                return (false, value);

            lastValue = value;
        }

        return (true, lastValue);
    }
}

public class OrNode : AstBoolOpNode
{
    internal static OrNode Shared { get; } = new();
    private OrNode() { }

    public override (bool Result, PyResult Value) GetBoolOpValue(PyCallContext context, IEnumerable<PyObject> values)
    {
        PyObject lastValue = null!;

        foreach (var value in values)
        {
            if (!PySpecialMethods.TryGetBool(context, value, out var b, out var result))
                return (false, result);

            if (b.BoolValue)
                return (true, value);

            lastValue = value;
        }

        return (false, lastValue);
    }
}
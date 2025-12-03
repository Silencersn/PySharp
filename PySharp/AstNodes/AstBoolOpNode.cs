using PySharp.PyObjects.Builtins;
using PySharp.PyRuntime;

namespace PySharp.AstNodes;

public abstract class AstBoolOpNode : AstNode
{
    public abstract (bool Result, PyObject? Value) GetBoolOpValue(IEnumerable<PyObject> values);
}

public class AndNode : AstBoolOpNode
{
    public static AndNode Shared { get; } = new();

    public override (bool Result, PyObject? Value) GetBoolOpValue(IEnumerable<PyObject> values)
    {
        PyObject lastValue = null!;

        foreach (var value in values)
        {
            if (!PySpecialMethods.TryGetBool(value, out var b))
                return (false, null);

            if (!b.BoolValue)
                return (false, value);

            lastValue = value;
        }

        return (true, lastValue);
    }
}

public class OrNode : AstBoolOpNode
{
    public static OrNode Shared { get; } = new();

    public override (bool Result, PyObject? Value) GetBoolOpValue(IEnumerable<PyObject> values)
    {
        PyObject lastValue = null!;

        foreach (var value in values)
        {
            if (!PySpecialMethods.TryGetBool(value, out var b))
                return (false, null);

            if (b.BoolValue)
                return (true, value);

            lastValue = value;
        }

        return (false, lastValue);
    }
}
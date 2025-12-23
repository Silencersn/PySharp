using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Diagnostics;
using System.Numerics;

namespace PySharp.PyModules.Random;

public partial class PyRandomObject : PyObject
{
    public static PyRandomObject Shared { get; } = new(System.Random.Shared);

    private readonly System.Random _random;

    public override PyTypeObject DefaultPyType => PyRandomObjectType.Shared;

    public PyRandomObject(System.Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        _random = random;
    }
}

public sealed class PyRandomObjectType : PyTypeObject<PyRandomObjectType, PyRandomObject>
{
    public override string Name => "Random";

    public PyRandomObjectType()
    {
        AppendMethodDescriptor("random", Random);
        AppendMethodDescriptor("uniform", Uniform);
        AppendMethodDescriptor("randrange", RandRange_1, RandRange_2);
        AppendMethodDescriptor("randint", RandInt);
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.RaiseTypeError(null);
        if (args.Count is 0)
            return new PyRandomObject(new System.Random()) { _pyType = cls };
        if (args.Count is 1)
        {
            if (!PySpecialMethods.TryGetIndex(context, args[0], out var seed, out var result))
                return result;
            return new PyRandomObject(new System.Random(seed.Int32Value)) { _pyType = cls };
        }
        return PyResult.RaiseTypeError(null);
    }

    [PyFunctionArgsDef()]
    internal PyResult Random(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        return PyFloatObject.FromDouble(self.PyRandom());
    }

    [PyFunctionArgsDef("a", "b")]
    internal PyResult Uniform(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        if (!PySpecialMethods.TryGetFloat(context, arguments[0], out var a, out var result))
            return result;
        if (!PySpecialMethods.TryGetFloat(context, arguments[1], out var b, out result))
            return result;
        return PyFloatObject.FromDouble(self.PyUniform(a.Value, b.Value));
    }

    [PyFunctionArgsDef("stop", "/")]
    internal PyResult RandRange_1(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        if (!PySpecialMethods.TryGetIndex(context, arguments[0], out var stop, out var result))
            return result;
        if (stop.Value <= 0)
            return PyResult.RaiseValueError("empty range for randrange()");
        var randResult = self.PyRandRange(BigInteger.Zero, stop.Value, BigInteger.One);
        Debug.Assert(randResult is not null);
        return PyIntObject.FromInteger(randResult.Value);
    }

    [PyFunctionArgsDef("start", "stop", "step=1", "/")]
    internal PyResult RandRange_2(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        if (!PySpecialMethods.TryGetIndex(context, arguments[0], out var startObj, out var result))
            return result;
        if (!PySpecialMethods.TryGetIndex(context, arguments[1], out var stopObj, out result))
            return result;
        if (!PySpecialMethods.TryGetIndex(context, arguments[2], out var stepObj, out result))
            return result;

        var (start, stop, step) = (startObj.Value, stopObj.Value, stepObj.Value);
        if (step == 0)
            return PyResult.RaiseValueError("zero step for randrange()");
        if (step > 0 && start >= stop || step < 0 && stop >= start)
        {
            if (step == 1)
                return PyResult.RaiseValueError($"empty range in randrange({start}, {stop})");
            return PyResult.RaiseValueError($"empty range in randrange({start}, {stop}, {step})");
        }
        var randResult = self.PyRandRange(start, stop, step);
        Debug.Assert(randResult is not null);
        return PyIntObject.FromInteger(randResult.Value);
    }

    [PyFunctionArgsDef("a", "b")]
    internal PyResult RandInt(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        if (!PySpecialMethods.TryGetIndex(context, arguments[0], out var a, out var result))
            return result;
        if (!PySpecialMethods.TryGetIndex(context, arguments[1], out var b, out result))
            return result;
        if (a.Value > b.Value)
            return PyResult.RaiseValueError($"empty range in randrange({a.Value}, {b.Value + 1})");
        var randResult = self.PyRandInt(a.Value, b.Value);
        Debug.Assert(randResult is not null);
        return PyIntObject.FromInteger(randResult.Value);
    }
}
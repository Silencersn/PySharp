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

    public override PyTypeObject PyType => PyRandomObjectType.Shared;

    public PyRandomObject(System.Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        _random = random;
    }

    [PyFunctionArgsDef()]
    internal PyFloatObject RandomImpl(PyArguments arguments)
    {
        return PyFloatObject.FromDouble(PyRandom());
    }

    [PyFunctionArgsDef("a", "b")]
    internal PyFloatObject? UniformImpl(PyArguments arguments)
    {
        if (!PyInteropService.TryGetFloat(arguments[0], out var a))
            return null;

        if (!PyInteropService.TryGetFloat(arguments[1], out var b))
            return null;

        return PyFloatObject.FromDouble(PyUniform(a, b));
    }

    [PyFunctionArgsDef("stop", "/")]
    internal PyObject? RandRangeImpl_1(PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out var stop))
            return null;

        if (stop <= 0)
            return PyVirtualMachine.RaiseValueError("empty range for randrange()");

        var result = PyRandRange(BigInteger.Zero, stop, BigInteger.One);
        Debug.Assert(result is not null);

        return PyIntObject.FromInteger(result.Value);
    }

    [PyFunctionArgsDef("start", "stop", "step=1", "/")]
    internal PyObject? RandRangeImpl_2(PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out var start))
            return null;

        if (!PyInteropService.TryGetIndex(arguments[1], out var stop))
            return null;

        if (!PyInteropService.TryGetIndex(arguments[2], out var step))
            return null;

        if (step == 0)
            return PyVirtualMachine.RaiseValueError("zero step for randrange()");

        if (step > 0 && start >= stop || step < 0 && stop >= start)
        {
            if (step == 1)
                return PyVirtualMachine.RaiseValueError($"empty range in randrange({start}, {stop})");

            return PyVirtualMachine.RaiseValueError($"empty range in randrange({start}, {stop}, {step})");
        }

        var result = PyRandRange(start, stop, step);
        Debug.Assert(result is not null);

        return PyIntObject.FromInteger(result.Value);
    }

    [PyFunctionArgsDef("a", "b")]
    internal PyObject? RandIntImpl(PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out var a))
            return null;

        if (!PyInteropService.TryGetIndex(arguments[1], out var b))
            return null;

        if (a > b)
            return PyVirtualMachine.RaiseValueError($"empty range in randrange({a}, {b + 1})");

        var result = PyRandInt(a, b);
        Debug.Assert(result is not null);

        return PyIntObject.FromInteger(result.Value);
    }
}

public sealed class PyRandomObjectType : PyTypeObject<PyRandomObjectType>
{
    public override string Name => "Random";

    public PyRandomObjectType()
    {
        AppendMethodDescriptor<PyRandomObject>("random", nameof(PyRandomObject.RandomImpl));
        AppendMethodDescriptor<PyRandomObject>("uniform", nameof(PyRandomObject.UniformImpl));
        AppendMethodDescriptor<PyRandomObject>("randrange", nameof(PyRandomObject.RandRangeImpl_1), nameof(PyRandomObject.RandRangeImpl_2));
        AppendMethodDescriptor<PyRandomObject>("randint", nameof(PyRandomObject.RandIntImpl));
    }

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyVirtualMachine.RaiseTypeError(null);

        if (args.Count is 0)
            return new PyRandomObject(new System.Random());

        if (args.Count is 1)
        {
            if (!PyInteropService.TryGetIndex(args[0], out var seed))
                return null;

            return new PyRandomObject(new System.Random(seed));
        }

        return PyVirtualMachine.RaiseTypeError(null);
    }
}
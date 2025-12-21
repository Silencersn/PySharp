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
            if (!PyInteropService.TryGetIndex(args[0], out int seed))
                return PyResult.CaptureExceptionFromPVM();
            return new PyRandomObject(new System.Random(seed)) { _pyType = cls };
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
        if (!PyInteropService.TryGetFloat(arguments[0], out var a))
            return PyResult.CaptureExceptionFromPVM();
        if (!PyInteropService.TryGetFloat(arguments[1], out var b))
            return PyResult.CaptureExceptionFromPVM();
        return PyFloatObject.FromDouble(self.PyUniform(a, b));
    }

    [PyFunctionArgsDef("stop", "/")]
    internal PyResult RandRange_1(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out BigInteger stop))
            return PyResult.CaptureExceptionFromPVM();
        if (stop <= 0)
            return PyResult.RaiseValueError("empty range for randrange()");
        var result = self.PyRandRange(BigInteger.Zero, stop, BigInteger.One);
        Debug.Assert(result is not null);
        return PyIntObject.FromInteger(result.Value);
    }

    [PyFunctionArgsDef("start", "stop", "step=1", "/")]
    internal PyResult RandRange_2(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out BigInteger start))
            return PyResult.CaptureExceptionFromPVM();
        if (!PyInteropService.TryGetIndex(arguments[1], out BigInteger stop))
            return PyResult.CaptureExceptionFromPVM();
        if (!PyInteropService.TryGetIndex(arguments[2], out BigInteger step))
            return PyResult.CaptureExceptionFromPVM();
        if (step == 0)
            return PyResult.RaiseValueError("zero step for randrange()");
        if (step > 0 && start >= stop || step < 0 && stop >= start)
        {
            if (step == 1)
                return PyResult.RaiseValueError($"empty range in randrange({start}, {stop})");
            return PyResult.RaiseValueError($"empty range in randrange({start}, {stop}, {step})");
        }
        var result = self.PyRandRange(start, stop, step);
        Debug.Assert(result is not null);
        return PyIntObject.FromInteger(result.Value);
    }

    [PyFunctionArgsDef("a", "b")]
    internal PyResult RandInt(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out BigInteger a))
            return PyResult.CaptureExceptionFromPVM();
        if (!PyInteropService.TryGetIndex(arguments[1], out BigInteger b))
            return PyResult.CaptureExceptionFromPVM();
        if (a > b)
            return PyResult.RaiseValueError($"empty range in randrange({a}, {b + 1})");
        var result = self.PyRandInt(a, b);
        Debug.Assert(result is not null);
        return PyIntObject.FromInteger(result.Value);
    }
}
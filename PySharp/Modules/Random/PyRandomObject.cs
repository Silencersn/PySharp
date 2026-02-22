using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Numerics;

namespace PySharp.Modules.Random;

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
    public override string DefaultModule => "random";
    public override string Name => "Random";

    public PyRandomObjectType()
    {
        AppendMethodDescriptor("random", Random);
        AppendMethodDescriptor("uniform", Uniform);
        AppendMethodDescriptor("randrange", RandRange_1, RandRange_2);
        AppendMethodDescriptor("randint", RandInt);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError(null);
        if (args.Count is 0)
            return new PyRandomObject(new System.Random()) { _pyType = cls };
        if (args.Count is 1)
        {
            var result = PySpecialMethods.Index(context, args[0]);
            if (result.IsError)
                return result;
            return new PyRandomObject(new System.Random(result.Value.Int32Value)) { _pyType = cls };
        }
        return PyResult.TypeError(null);
    }

    [PyFunctionArgsDef()]
    internal PyResult Random(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        return PyFloatObject.FromDouble(self.PyRandom());
    }

    [PyFunctionArgsDef("a", "b")]
    internal PyResult Uniform(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        var aResult = PySpecialMethods.Float(context, arguments[0]);
        if (aResult.IsError)
            return aResult;
        var bResult = PySpecialMethods.Float(context, arguments[1]);
        if (bResult.IsError)
            return bResult;
        return PyFloatObject.FromDouble(self.PyUniform(aResult.Value.Value, bResult.Value.Value));
    }

    [PyFunctionArgsDef("stop", "/")]
    internal PyResult RandRange_1(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;
        if (result.Value.Value <= 0)
            return PyResult.ValueError(PySR.Runtime_Random_EmptyRangeForRandRange);
        var randResult = self.PyRandRange(BigInteger.Zero, result.Value.Value, BigInteger.One);
        Debug.Assert(randResult is not null);
        return PyIntObject.FromInteger(randResult.Value);
    }

    [PyFunctionArgsDef("start", "stop", "step=1", "/")]
    internal PyResult RandRange_2(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        var startResult = PySpecialMethods.Index(context, arguments[0]);
        if (startResult.IsError)
            return startResult;
        var stopResult = PySpecialMethods.Index(context, arguments[1]);
        if (stopResult.IsError)
            return stopResult;
        var stepResult = PySpecialMethods.Index(context, arguments[2]);
        if (stepResult.IsError)
            return stepResult;

        var (start, stop, step) = (startResult.Value.Value, stopResult.Value.Value, stepResult.Value.Value);
        if (step == 0)
            return PyResult.ValueError(PySR.Runtime_Random_ZeroStepForRandRange);
        if (step > 0 && start >= stop || step < 0 && stop >= start)
        {
            if (step == 1)
                return PyResult.ValueError(PySR.Runtime_Random_EmptyRangeInRandRange2Args, start, stop);
            return PyResult.ValueError(PySR.Runtime_Random_EmptyRangeInRandRange3Args, start, stop, step);
        }
        var randResult = self.PyRandRange(start, stop, step);
        Debug.Assert(randResult is not null);
        return PyIntObject.FromInteger(randResult.Value);
    }

    [PyFunctionArgsDef("a", "b")]
    internal PyResult RandInt(PyCallContext context, PyRandomObject self, PyArguments arguments)
    {
        var aResult = PySpecialMethods.Index(context, arguments[0]);
        if (aResult.IsError)
            return aResult;
        var bResult = PySpecialMethods.Index(context, arguments[1]);
        if (bResult.IsError)
            return bResult;
        var (a, b) = (aResult.Value, bResult.Value);
        if (a.Value > b.Value)
            return PyResult.ValueError(PySR.Runtime_Random_EmptyRangeInRandRange2Args, a.Value, b.Value + 1);
        var randResult = self.PyRandInt(a.Value, b.Value);
        Debug.Assert(randResult is not null);
        return PyIntObject.FromInteger(randResult.Value);
    }
}
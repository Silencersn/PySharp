using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public class PySliceObject : PyObject
{
    public PyObject Start { get; }
    public PyObject Stop { get; }
    public PyObject Step { get; }

    public override PyTypeObject DefaultPyType => PySliceObjectType.Shared;

    public PySliceObject(PyObject start, PyObject stop, PyObject step)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(stop);
        ArgumentNullException.ThrowIfNull(step);
        Start = start;
        Stop = stop;
        Step = step;
    }
}

[PyType("slice")]
public sealed partial class PySliceObjectType : PyTypeObject<PySliceObject>
{
    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl_1, NewImpl_2);

    [PyFunctionArgsDef("stop", "/")]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        return new PySliceObject(PyNoneObject.None, arguments[0], PyNoneObject.None);
    }

    [PyFunctionArgsDef("start", "stop", "step=None", "/")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        return new PySliceObject(arguments[0], arguments[1], arguments[2]);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    [PyProperty("start")]
    private static PyResult Get_Start(PyCallContext context, PySliceObject self)
    {
        return self.Start;
    }
    [PyProperty("stop")]
    private static PyResult Get_Stop(PyCallContext context, PySliceObject self)
    {
        return self.Stop;
    }
    [PyProperty("step")]
    private static PyResult Get_Step(PyCallContext context, PySliceObject self)
    {
        return self.Step;
    }
}

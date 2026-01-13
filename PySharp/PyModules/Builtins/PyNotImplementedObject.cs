using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyNotImplementedObject : PyObject
{
    public static PyNotImplementedObject NotImplemented { get; } = new PyNotImplementedObject();
    public override PyTypeObject DefaultPyType => PyNotImplementedObjectType.Shared;
    private PyNotImplementedObject() { }
}

public sealed class PyNotImplementedObjectType : PyTypeObject<PyNotImplementedObjectType, PyNotImplementedObject>
{
    public override string Module => "builtins";
    public override string Name => "NotImplementedType";
    public override bool IsSealed => true;
    private static readonly PyStrObject _repr = PyStrObject.FromString("NotImplemented");

    protected override PyResult Repr(PyCallContext context, PyNotImplementedObject self)
    {
        return _repr;
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateEmpty(args, kwargs, out var err))
            return err.Value;
        return PyNotImplementedObject.NotImplemented;
    }

    protected override PyResult Bool(PyCallContext context, PyNotImplementedObject self)
    {
        context.TryWarn<PyDeprecationWarningObjectType>("NotImplemented should not be used in a boolean context");
        return base.Bool(context, self);
    }
}
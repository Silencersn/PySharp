using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

public sealed class PyFunctionObject : PyObject, IPyObjectName
{
    private readonly PyUncompoundedFunction _function;
    private readonly PyCellObject[]? _closure;

    public string Name { get; }
    internal ReadOnlySpan<PyCellObject> CapturedVariables => _closure;

    public override PyTypeObject PyType => PyBuiltinTypes.Function;

    public PyFunctionObject(string name, PyUncompoundedFunction function, PyCellObject[]? closure)
    {
        Name = name;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        _function = function;
        _closure = closure;
        if (closure is not null)
            PyAttributes.Add(PySpecialNames.Closure, PyTupleObject.CreateProxy(closure));
        else
            PyAttributes.Add(PySpecialNames.Closure, PyNoneObject.None);
    }

    public override PyObject? Repr()
    {
        return PyStrObject.FromString($"<function {Name} at 0x{PyId:X16}>");
    }

    public override PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _function.Invoke(args, kwargs);
    }

    public override PyObject? Get(PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return this;

        return new PyMethodObject(this, instance);
    }
}

public sealed class PyFunctionObjectType : PyPrimitiveTypeObject<PyFunctionObjectType, PyFunctionObject>
{
    public override string Name => "function";
}
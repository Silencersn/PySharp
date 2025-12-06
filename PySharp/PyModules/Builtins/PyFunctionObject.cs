using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

public class PyFunctionObject : PyObject, IPyObjectName
{
    private readonly PyUncompoundedFunction _function;

    public string Name { get; }

    public override PyTypeObject PyType => PyBuiltinTypes.Function;

    public PyFunctionObject(string name, PyUncompoundedFunction function)
    {
        Name = name;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        _function = function;
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

internal sealed class PyFunctionObjectType : PyTypeObject
{
    public override string Name => "function";

    public PyFunctionObjectType()
    {
        AppendSpecialMethodsAsDescriptors<PyFunctionObject>();
    }
}
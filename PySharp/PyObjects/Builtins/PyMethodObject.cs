using PySharp.PyRuntime;

namespace PySharp.PyObjects.Builtins;

public class PyMethodObject : PyObject
{
    private readonly PyFunctionObject _functionObj;
    private readonly PyObject _target;

    public override PyTypeObject PyType => PyBuiltinTypes.Method;
    public string Name { get; }

    internal PyMethodObject(PyFunctionObject functionObj, PyObject target)
    {
        _functionObj = functionObj;
        _target = target;
        Name = functionObj.Name;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
    }

    public override PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _functionObj.Call([_target, .. args], kwargs);
    }
}

public sealed class PyMethodObjectType : PyTypeObject
{
    public override string Name => "method";
}
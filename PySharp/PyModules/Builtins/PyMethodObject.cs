namespace PySharp.PyModules.Builtins;

public sealed class PyMethodObject : PyObject
{
    internal readonly PyFunctionObject _functionObj;
    internal readonly PyObject _target;

    public override PyTypeObject PyType => PyMethodObjectType.Shared;

    internal PyMethodObject(PyFunctionObject functionObj, PyObject target)
    {
        _functionObj = functionObj;
        _target = target;
    }

    public override PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _functionObj.Call([_target, .. args], kwargs);
    }

    public override PyObject? GetAttr(string item)
    {
        return _functionObj.GetAttribute(item);
    }
}

public sealed class PyMethodObjectType : PyPrimitiveTypeObject<PyMethodObjectType, PyMethodObject>
{
    public override string Name => "method";

    public PyMethodObjectType()
    {
        AppendMemberDescriptor<PyMethodObject>("__func__", method => method._functionObj);
        AppendMemberDescriptor<PyMethodObject>("__self__", method => method._target);
    }
}
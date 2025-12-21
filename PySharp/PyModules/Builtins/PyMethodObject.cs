using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public sealed class PyMethodObject : PyObject
{
    internal readonly PyFunctionObject _functionObj;
    internal readonly PyObject _target;

    public override PyTypeObject DefaultPyType => PyMethodObjectType.Shared;

    internal PyMethodObject(PyFunctionObject functionObj, PyObject target)
    {
        _functionObj = functionObj;
        _target = target;
    }
}

public sealed class PyMethodObjectType : PyTypeObject<PyMethodObjectType, PyMethodObject>
{
    public override string Name => "method";

    public PyMethodObjectType()
    {
        AppendMemberDescriptor<PyMethodObject>("__func__", method => method._functionObj);
        AppendMemberDescriptor<PyMethodObject>("__self__", method => method._target);
    }

    protected internal override PyResult Call(PyCallContext context, PyMethodObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return self._functionObj.Call([self._target, .. args], kwargs) ?? PyResult.CaptureExceptionFromPVM();
    }

    protected internal override PyResult GetAttr(PyCallContext context, PyMethodObject self, string item)
    {
        var result = self._functionObj.GetAttribute(item);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }
}
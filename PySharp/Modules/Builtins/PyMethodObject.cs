using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public sealed class PyMethodObject : PyObject
{
    internal readonly PyObject _functionObj;
    internal readonly PyObject _target;

    public override PyTypeObject DefaultPyType => PyMethodObjectType.Shared;

    internal PyMethodObject(PyObject functionObj, PyObject target)
    {
        _functionObj = functionObj;
        _target = target;
    }
}

[PyType("method")]
public sealed partial class PyMethodObjectType : PyTypeObject<PyMethodObject>
{
    protected override PyResult Call(PyCallContext context, PyMethodObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return self._functionObj.Call(context, [self._target, .. args], kwargs);
    }

    protected override PyResult GetAttr(PyCallContext context, PyMethodObject self, PyObject item)
    {
        return PyOperators.GetAttr(context, self._functionObj, item);
    }

    protected override PyResult Repr(PyCallContext context, PyMethodObject self)
    {
        // Try to get __qualname__ from the function
        var qualnameAttr = PyOperators.GetAttr(context, self._functionObj, PySpecialNames.QualName);
        string funcName;
        if (qualnameAttr.IsSuccessful && qualnameAttr.Value is PyStrObject qualnameStr)
            funcName = qualnameStr.Value;
        else
            funcName = "?";

        // Try to get the repr of target
        var targetRepr = PySpecialMethods.Repr(context, self._target);
        string targetStr;
        if (targetRepr.IsSuccessful)
            targetStr = targetRepr.Value.Value;
        else
            targetStr = "?";

        return PyStrObject.FromString($"<bound method {funcName} of {targetStr}>");
    }

    // method_richcompare: only ==/!= are supported, both sides must be
    // bound methods, and __func__/__self__ compare by identity
    protected override PyResult Eq(PyCallContext context, PyMethodObject self, PyObject other)
    {
        if (other is not PyMethodObject otherMethod)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(
            ReferenceEquals(self._functionObj, otherMethod._functionObj) &&
            ReferenceEquals(self._target, otherMethod._target));
    }

    protected override PyResult Ne(PyCallContext context, PyMethodObject self, PyObject other)
    {
        if (other is not PyMethodObject otherMethod)
            return PyNotImplementedObject.NotImplemented;
        return PyBoolObject.FromBoolean(
            !ReferenceEquals(self._functionObj, otherMethod._functionObj) ||
            !ReferenceEquals(self._target, otherMethod._target));
    }

    protected override PyResult Hash(PyCallContext context, PyMethodObject self)
    {
        // method_hash: identity hash of __self__ (PyObject_GenericHash
        // bypasses a custom __hash__) combined with the hash of __func__
        var funcHash = PySpecialMethods.Hash(context, self._functionObj);
        if (funcHash.IsError)
            return funcHash;
        return PyIntObject.FromInteger(self._target.GetHashCode() ^ funcHash.Value.Int32Value);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError("method() does not accept keyword arguments");

        if (args.Count is not 2)
            return PyResult.TypeError($"method() takes exactly 2 arguments ({args.Count} given)");

        var function = args[0];
        var instance = args[1];

        // Validate callable
        var callAttr = PyOperators.GetAttr(context, function, PySpecialNames.Interned.Call);
        if (callAttr.IsError && !callAttr.IsAttributeError)
            return callAttr;
        if (callAttr.IsAttributeError)
            return PyResult.TypeError(PySR.Format(PySR.Runtime_Object_NonCallable, function.PyType.FullName));

        // Validate instance is not None (CPython allows None but creates unbound method)
        if (instance is PyNoneObject)
            return PyResult.TypeError("instance must not be None");

        return new PyMethodObject(function, instance);
    }

    [PyProperty(PySpecialNames.Func)]
    private static PyResult Get_Func(PyCallContext context, PyMethodObject self)
    {
        return self._functionObj;
    }

    [PyProperty(PySpecialNames.Self)]
    private static PyResult Get_Self(PyCallContext context, PyMethodObject self)
    {
        return self._target;
    }

    [PyProperty(PySpecialNames.Doc)]
    private static PyResult Get_Doc(PyCallContext context, PyMethodObject self)
    {
        return PyOperators.GetAttr(context, self._functionObj, PySpecialNames.Doc);
    }
}

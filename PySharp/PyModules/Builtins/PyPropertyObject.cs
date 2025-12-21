using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Collections.Frozen;

namespace PySharp.PyModules.Builtins;

public sealed class PyPropertyObject : PyObject, IPyDescriptor
{
    internal PyObject _fget;
    internal PyObject _fset;
    internal PyObject _fdel;
    internal PyObject _doc;

    public override PyTypeObject DefaultPyType => PyPropertyObjectType.Shared;

    bool IPyDescriptor.SupportsGet => true;
    bool IPyDescriptor.SupportsSet => true;
    bool IPyDescriptor.SupportsDelete => true;

    public PyPropertyObject(PyObject fget, PyObject fset, PyObject fdel, PyObject doc)
    {
        _fget = fget;
        _fset = fset;
        _fdel = fdel;
        _doc = doc;
    }

    [PyFunctionArgsDef("fget")]
    internal PyPropertyObject GetterImpl(PyArguments arguments)
    {
        _fget = arguments[0];
        return this;
    }

    [PyFunctionArgsDef("fset")]
    internal PyPropertyObject SetterImpl(PyArguments arguments)
    {
        _fset = arguments[0];
        return this;
    }

    [PyFunctionArgsDef("deleter")]
    internal PyPropertyObject DeleterImpl(PyArguments arguments)
    {
        _fdel = arguments[0];
        return this;
    }
}

public sealed class PyPropertyObjectType : PyTypeObject<PyPropertyObjectType, PyPropertyObject>
{
    public override string Name => "property";

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl);

    public PyPropertyObjectType()
    {
        AppendMethodDescriptor<PyPropertyObject>("getter", nameof(PyPropertyObject.GetterImpl));
        AppendMethodDescriptor<PyPropertyObject>("setter", nameof(PyPropertyObject.SetterImpl));
        AppendMethodDescriptor<PyPropertyObject>("deleter", nameof(PyPropertyObject.DeleterImpl));
    }

    [PyFunctionArgsDef("fget=None", "fset=None", "fdel=None", "doc=None")]
    private static PyPropertyObject NewImpl(PyArguments arguments)
    {
        return new PyPropertyObject(arguments[0], arguments[1], arguments[2], arguments[3]);
    }

    protected internal override PyResult Get(PyCallContext context, PyPropertyObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return self;
        var result = self._fget.Call([instance], FrozenDictionary<string, PyObject>.Empty);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }

    protected internal override PyResult Set(PyCallContext context, PyPropertyObject self, PyObject instance, PyObject value)
    {
        var result = self._fset.Call([instance, value], FrozenDictionary<string, PyObject>.Empty);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }

    protected internal override PyResult Delete(PyCallContext context, PyPropertyObject self, PyObject instance)
    {
        var result = self._fdel.Call([instance], FrozenDictionary<string, PyObject>.Empty);
        if (result is null)
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(args, kwargs);
        if (obj is null)
            return PyResult.CaptureExceptionFromPVM();
        obj._pyType = cls;
        return obj;
    }
}

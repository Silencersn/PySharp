using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Collections.Frozen;

namespace PySharp.PyModules.Builtins;

public sealed class PyPropertyObject : PyObject, IPyDescriptor
{
    private PyObject _fget;
    private PyObject _fset;
    private PyObject _fdel;
    private PyObject _doc;

    public override PyTypeObject PyType => PyBuiltinTypes.Property;

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

    public override PyObject? Get(PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return this;

        return _fget.Call([instance], FrozenDictionary<string, PyObject>.Empty);
    }

    public override PyObject? Set(PyObject instance, PyObject value)
    {
        return _fset.Call([instance, value], FrozenDictionary<string, PyObject>.Empty);
    }

    public override PyObject? Delete(PyObject instance)
    {
        return _fdel.Call([instance], FrozenDictionary<string, PyObject>.Empty);
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

public sealed class PyPropertyObjectType : PyPrimitiveTypeObject<PyPropertyObjectType, PyPropertyObject>
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

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(args, kwargs);
    }
}

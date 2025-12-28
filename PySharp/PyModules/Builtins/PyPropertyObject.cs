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
}

public sealed class PyPropertyObjectType : PyTypeObject<PyPropertyObjectType, PyPropertyObject>
{
    public override string Name => "property";

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    public PyPropertyObjectType()
    {
        AppendMethodDescriptor("getter", Getter);
        AppendMethodDescriptor("setter", Setter);
        AppendMethodDescriptor("deleter", Deleter);
    }

    [PyFunctionArgsDef("fget")]
    internal PyResult Getter(PyCallContext context, PyPropertyObject self, PyArguments arguments)
    {
        self._fget = arguments[0];
        return self;
    }

    [PyFunctionArgsDef("fset")]
    internal PyResult Setter(PyCallContext context, PyPropertyObject self, PyArguments arguments)
    {
        self._fset = arguments[0];
        return self;
    }

    [PyFunctionArgsDef("deleter")]
    internal PyResult Deleter(PyCallContext context, PyPropertyObject self, PyArguments arguments)
    {
        self._fdel = arguments[0];
        return self;
    }

    [PyFunctionArgsDef("fget=None", "fset=None", "fdel=None", "doc=None")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        return new PyPropertyObject(arguments[0], arguments[1], arguments[2], arguments[3]);
    }

    protected internal override PyResult Get(PyCallContext context, PyPropertyObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
            return self;
        return self._fget.Call(context, [instance], FrozenDictionary<string, PyObject>.Empty);
    }

    protected internal override PyResult Set(PyCallContext context, PyPropertyObject self, PyObject instance, PyObject value)
    {
        return self._fset.Call(context, [instance, value], FrozenDictionary<string, PyObject>.Empty);
    }

    protected internal override PyResult Delete(PyCallContext context, PyPropertyObject self, PyObject instance)
    {
        return self._fdel.Call(context, [instance], FrozenDictionary<string, PyObject>.Empty);
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(context, args, kwargs);
    }
}

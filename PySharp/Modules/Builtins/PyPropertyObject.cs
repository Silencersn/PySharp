using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.PyAttributes;
using System.Collections.Frozen;

namespace PySharp.Modules.Builtins;

public sealed class PyPropertyObject : PyObject
{
    internal PyObject _fget;
    internal PyObject _fset;
    internal PyObject _fdel;
    internal PyObject _doc;

    public override PyTypeObject DefaultPyType => PyPropertyObjectType.Shared;

    public PyPropertyObject(PyObject fget, PyObject fset, PyObject fdel, PyObject doc)
    {
        _fget = fget;
        _fset = fset;
        _fdel = fdel;
        _doc = doc;
    }
}

[PyType("property")]
public sealed partial class PyPropertyObjectType : PyTypeObject<PyPropertyObject>
{

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    [PyMethod("getter")]
    [PyFunctionArgsDef("fget")]
    private static PyResult Getter(PyCallContext context, PyPropertyObject self, PyArguments arguments)
    {
        self._fget = arguments[0];
        return self;
    }

    [PyMethod("setter")]
    [PyFunctionArgsDef("fset")]
    private static PyResult Setter(PyCallContext context, PyPropertyObject self, PyArguments arguments)
    {
        self._fset = arguments[0];
        return self;
    }

    [PyMethod("deleter")]
    [PyFunctionArgsDef("deleter")]
    private static PyResult Deleter(PyCallContext context, PyPropertyObject self, PyArguments arguments)
    {
        self._fdel = arguments[0];
        return self;
    }

    [PyFunctionArgsDef("fget=None", "fset=None", "fdel=None", "doc=None")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        return new PyPropertyObject(arguments[0], arguments[1], arguments[2], arguments[3]);
    }

    protected override PyResult Get(PyCallContext context, PyPropertyObject self, PyObject instance, PyObject owner)
    {
        if (instance is PyNoneObject)
        {
            if (owner is PyNoneObject)
                return PyResult.TypeError(PySR.Runtime_Descriptor_GetNoneNoneInvalid);

            return self;
        }

        return self._fget.Call(context, [instance], FrozenDictionary<string, PyObject>.Empty);
    }

    protected override PyResult Set(PyCallContext context, PyPropertyObject self, PyObject instance, PyObject value)
    {
        return self._fset.Call(context, [instance, value], FrozenDictionary<string, PyObject>.Empty);
    }

    protected override PyResult Delete(PyCallContext context, PyPropertyObject self, PyObject instance)
    {
        return self._fdel.Call(context, [instance], FrozenDictionary<string, PyObject>.Empty);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(context, args, kwargs);
    }
}

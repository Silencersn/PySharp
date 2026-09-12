using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Collections.Frozen;

namespace PySharp.Modules.Builtins;

public sealed class PyPropertyObject : PyObject
{
    internal PyObject _fget;
    internal PyObject _fset;
    internal PyObject _fdel;
    internal PyObject _doc;
    internal string? _name;
    // the doc was inherited from fget: a later getter() re-derives it,
    // while an explicit doc argument is kept as-is (CPython getter_doc)
    internal bool _docInherited = true;

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

    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyMethod("getter")]
    [PyFunctionParameters("fget")]
    private static PyResult Getter(PyCallContext context, PyPropertyObject self, PyArguments arguments)
    {
        // CPython property_getter returns a copy; the original property is
        // untouched. A doc inherited from the previous getter is re-derived
        // from the new one, an explicit doc is kept.
        var inherited = self._docInherited;
        var doc = self._doc;
        if (inherited)
            doc = DocOf(context, arguments[0]);
        return Copy(self, arguments[0], self._fset, self._fdel, doc, inherited);
    }

    [PyMethod("setter")]
    [PyFunctionParameters("fset")]
    private static PyResult Setter(PyCallContext context, PyPropertyObject self, PyArguments arguments)
    {
        return Copy(self, self._fget, arguments[0], self._fdel, self._doc, self._docInherited);
    }

    [PyMethod("deleter")]
    [PyFunctionParameters("deleter")]
    private static PyResult Deleter(PyCallContext context, PyPropertyObject self, PyArguments arguments)
    {
        return Copy(self, self._fget, self._fset, arguments[0], self._doc, self._docInherited);
    }

    [PyFunctionParameters("fget=None", "fset=None", "fdel=None", "doc=None")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        // property_init: a missing doc argument is taken from fget.__doc__
        var doc = arguments[3];
        var inherited = doc is PyNoneObject;
        if (inherited)
            doc = DocOf(context, arguments[0]);
        return new PyPropertyObject(arguments[0], arguments[1], arguments[2], doc)
        {
            _docInherited = inherited,
        };
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

    // property_set / property_delete: a missing fset/fdel is an
    // AttributeError naming the property and the owner type, never a
    // None-callable TypeError
    protected override PyResult Set(PyCallContext context, PyPropertyObject self, PyObject instance, PyObject value)
    {
        if (self._fset is PyNoneObject)
            return PyResult.AttributeError(PySR.Runtime_Property_NoSetter, DisplayName(context, self), instance.PyType.FullName);
        return self._fset.Call(context, [instance, value], FrozenDictionary<string, PyObject>.Empty);
    }

    protected override PyResult Delete(PyCallContext context, PyPropertyObject self, PyObject instance)
    {
        if (self._fdel is PyNoneObject)
            return PyResult.AttributeError(PySR.Runtime_Property_NoDeleter, DisplayName(context, self), instance.PyType.FullName);
        return self._fdel.Call(context, [instance], FrozenDictionary<string, PyObject>.Empty);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(context, args, kwargs);
    }

    // property___set_name__: records the attribute name for __name__ and
    // the no-setter/no-deleter messages
    protected override PyResult SetName(PyCallContext context, PyPropertyObject self, PyObject owner, PyObject name)
    {
        if (name is PyStrObject str)
            self._name = str.Value;
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Name)]
    private static PyResult Get_Name(PyCallContext context, PyPropertyObject self)
    {
        return PyStrObject.FromString(DisplayName(context, self));
    }

    [PyProperty(PySpecialNames.Doc)]
    private static PyResult Get_Doc(PyCallContext context, PyPropertyObject self)
    {
        return self._doc;
    }

    [PyProperty("fget")]
    private static PyResult Get_Fget(PyCallContext context, PyPropertyObject self)
    {
        return self._fget;
    }

    [PyProperty("fset")]
    private static PyResult Get_Fset(PyCallContext context, PyPropertyObject self)
    {
        return self._fset;
    }

    [PyProperty("fdel")]
    private static PyResult Get_Fdel(PyCallContext context, PyPropertyObject self)
    {
        return self._fdel;
    }

    private static PyPropertyObject Copy(PyPropertyObject source, PyObject fget, PyObject fset, PyObject fdel, PyObject doc, bool docInherited)
    {
        return new PyPropertyObject(fget, fset, fdel, doc)
        {
            _name = source._name,
            _docInherited = docInherited,
        };
    }

    private static PyObject DocOf(PyCallContext context, PyObject fget)
    {
        if (fget is PyNoneObject)
            return PyNoneObject.None;
        var docResult = PyOperators.GetAttr(context, fget, PySpecialNames.Doc);
        return docResult.IsSuccessful ? docResult.Value : PyNoneObject.None;
    }

    // the getter's __name__ doubles as the property name in messages
    // (CPython records it via __set_name__; for decorated getters both
    // coincide, and unrecorded properties show '<lambda>' the same way)
    private static string DisplayName(PyCallContext context, PyPropertyObject self)
    {
        if (self._name is not null)
            return self._name;
        if (self._fget is not PyNoneObject)
        {
            var nameResult = PyOperators.GetAttr(context, self._fget, PySpecialNames.Name);
            if (nameResult.IsSuccessful && nameResult.Value is PyStrObject name)
                return name.Value;
        }
        return string.Empty;
    }
}

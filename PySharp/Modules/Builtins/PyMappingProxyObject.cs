using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

// CPython mappingproxyobject (Objects/descrobject.c): a read-only view over
// a mapping. Every read and the narrow method face delegate to the wrapped
// mapping; mutations stay on the owner's setattr/delattr protocol, so a
// class namespace cannot be rewritten through this view
internal sealed class PyMappingProxyObject : PyObject
{
    internal PyObject Mapping { get; }

    public override PyTypeObject DefaultPyType => PyMappingProxyObjectType.Shared;

    internal PyMappingProxyObject(PyObject mapping)
    {
        Mapping = mapping;
    }
}

[PyType("mappingproxy", IsSealed = true)]
internal sealed partial class PyMappingProxyObjectType : PyTypeObject<PyMappingProxyObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(context, args, kwargs);
    }

    [PyFunctionParameters("mapping")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var mapping = arguments[0];

        // CPython mappingproxy_check_mapping: PyMapping_Check (mp_subscript
        // present) minus list and tuple; a nested mappingproxy wraps as-is
        if (mapping.PyType.Slots.GetItem is null
            || PyListObjectType.Shared.IsInstance(mapping)
            || PyTupleObjectType.Shared.IsInstance(mapping))
            return PyResult.TypeError(PySR.Runtime_MappingProxy_ArgMustBeMapping, mapping.PyType.TpName);

        return new PyMappingProxyObject(mapping);
    }

    // CPython mappingproxy_repr: mappingproxy(%R)
    protected override PyResult Repr(PyCallContext context, PyMappingProxyObject self)
    {
        var inner = PySpecialMethods.Repr(context, self.Mapping);
        if (inner.IsError)
            return inner;

        return PyStrObject.FromString($"mappingproxy({inner.Value.Value})");
    }

    // CPython mappingproxy_str delegates to the wrapped mapping, so a proxy
    // prints like the dict it wraps
    protected override PyResult Str(PyCallContext context, PyMappingProxyObject self)
    {
        return PySpecialMethods.Str(context, self.Mapping);
    }

    // CPython mappingproxy_hash delegates; a dict mapping reports
    // "unhashable type: 'dict'"
    protected override PyResult Hash(PyCallContext context, PyMappingProxyObject self)
    {
        return PySpecialMethods.Hash(context, self.Mapping);
    }

    protected override PyResult Len(PyCallContext context, PyMappingProxyObject self)
    {
        return PySpecialMethods.Len(context, self.Mapping);
    }

    // CPython mappingproxy_getitem: PyObject_GetItem on the wrapped mapping
    protected override PyResult GetItem(PyCallContext context, PyMappingProxyObject self, PyObject key)
    {
        return PySpecialMethods.GetItem(context, self.Mapping, key);
    }

    // CPython leaves mp_ass_subscript empty; the del path reports "does not"
    // (mapping wording) where the sequence fallback says "doesn't"
    protected override PyResult DelItem(PyCallContext context, PyMappingProxyObject self, PyObject key)
    {
        return PyResult.TypeError(PySR.Runtime_Mapping_ItemDeletionNotSupported, self.PyType.TpName);
    }

    // SetItem stays unwired: the generic slot-less fallback is already the
    // mapping wording "does not support item assignment"

    protected override PyResult Contains(PyCallContext context, PyMappingProxyObject self, PyObject item)
    {
        return PySpecialMethods.Contains(context, self.Mapping, item);
    }

    // CPython mappingproxy_getiter: PyObject_GetIter on the wrapped mapping
    protected override PyResult Iter(PyCallContext context, PyMappingProxyObject self)
    {
        return PySpecialMethods.Iter(context, self.Mapping);
    }

    // CPython mappingproxy_reversed: call the wrapped mapping's __reversed__
    protected override PyResult Reversed(PyCallContext context, PyMappingProxyObject self)
    {
        return self.Mapping.CallMethod(context, PySpecialNames.Reversed);
    }

    // CPython mappingproxy_richcompare: every comparison runs against the
    // wrapped mapping, so failure messages name the underlying types
    protected override PyResult Eq(PyCallContext context, PyMappingProxyObject self, PyObject other)
    {
        return PyOperators.Eq(context, self.Mapping, other);
    }

    protected override PyResult Ne(PyCallContext context, PyMappingProxyObject self, PyObject other)
    {
        return PyOperators.NotEq(context, self.Mapping, other);
    }

    protected override PyResult Lt(PyCallContext context, PyMappingProxyObject self, PyObject other)
    {
        return PyOperators.Lt(context, self.Mapping, other);
    }

    protected override PyResult Le(PyCallContext context, PyMappingProxyObject self, PyObject other)
    {
        return PyOperators.LtE(context, self.Mapping, other);
    }

    protected override PyResult Gt(PyCallContext context, PyMappingProxyObject self, PyObject other)
    {
        return PyOperators.Gt(context, self.Mapping, other);
    }

    protected override PyResult Ge(PyCallContext context, PyMappingProxyObject self, PyObject other)
    {
        return PyOperators.GtE(context, self.Mapping, other);
    }

    // CPython mappingproxy_or: unwrap either side's proxy, then PyNumber_Or
    protected override PyResult Or(PyCallContext context, PyMappingProxyObject self, PyObject other)
    {
        return PyOperators.BitOr(context, self.Mapping, Unwrap(other));
    }

    protected override PyResult ROr(PyCallContext context, PyMappingProxyObject self, PyObject other)
    {
        return PyOperators.BitOr(context, other, self.Mapping);
    }

    // CPython mappingproxy_ior: |= is rejected with a use-| hint
    protected override PyResult IOr(PyCallContext context, PyMappingProxyObject self, PyObject other)
    {
        return PyResult.TypeError(PySR.Runtime_MappingProxy_InPlaceOrNotSupported, self.PyType.TpName);
    }

    private static PyObject Unwrap(PyObject obj)
    {
        return obj is PyMappingProxyObject proxy ? proxy.Mapping : obj;
    }

    // CPython mappingproxy methods vectorcall the wrapped mapping's own
    // method, so custom mappings answer with whatever they implement
    [PyMethod("get")]
    [PyFunctionParameters("key", "default=None", "/")]
    private static PyResult Get(PyCallContext context, PyMappingProxyObject self, PyArguments arguments)
    {
        return self.Mapping.CallMethod(context, "get", [arguments[0], arguments[1]]);
    }

    [PyMethod("keys")]
    [PyFunctionParameters()]
    private static PyResult Keys(PyCallContext context, PyMappingProxyObject self, PyArguments arguments)
    {
        return self.Mapping.CallMethod(context, "keys");
    }

    [PyMethod("values")]
    [PyFunctionParameters()]
    private static PyResult Values(PyCallContext context, PyMappingProxyObject self, PyArguments arguments)
    {
        return self.Mapping.CallMethod(context, "values");
    }

    [PyMethod("items")]
    [PyFunctionParameters()]
    private static PyResult Items(PyCallContext context, PyMappingProxyObject self, PyArguments arguments)
    {
        return self.Mapping.CallMethod(context, "items");
    }

    [PyMethod("copy")]
    [PyFunctionParameters()]
    private static PyResult Copy(PyCallContext context, PyMappingProxyObject self, PyArguments arguments)
    {
        return self.Mapping.CallMethod(context, "copy");
    }
}

using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

[PyType("dict")]
public sealed partial class PyDictObjectType : PyTypeObject<PyDictObject>
{
    static PyDictObjectType()
    {
        // CPython add_operators: unhashable types carry __hash__ = None in
        // the type dict (read face) while tp_hash raises the TypeError
        Shared.PyAttributes[PySpecialNames.Hash] = PyNoneObject.None;
    }

    // CPython PyObject_HashNotImplemented
    protected override PyResult Hash(PyCallContext context, PyDictObject self)
    {
        return PyResult.TypeError(PySR.Runtime_Object_Unhashable, self.PyType.Name);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython dict_new: allocate an empty dict and ignore all arguments;
        // they are consumed by dict_init, which a subclass __init__ replaces
        return new PyDictObject { _pyType = cls };
    }

    protected override PyResult Init(PyCallContext context, PyDictObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython dict_init -> dict_update_common: at most one positional
        // source plus keyword updates
        if (args.Count > 1)
            return PyResult.TypeError(PySR.Runtime_Dictionary_ExpectedAtMostOne, args.Count);

        if (args.Count is 1)
        {
            var result = self.Update(context, args[0]);
            if (result.IsError)
                return result;
        }

        foreach (var pair in kwargs)
            self.SetItem(pair.Key, pair.Value);

        return PyNoneObject.None;
    }

    protected override PyResult GetItem(PyCallContext context, PyDictObject self, PyObject item)
    {
        var value = self.GetItem(context, item);
        if (value.IsSuccessful || value.Exception.PyType != PyKeyErrorObjectType.Shared)
            return value;

        var missing = self.PyType.Slots.Missing;
        if (missing is null)
            return PyResult.KeyError(item);

        return missing(context, self, item);
    }

    protected override PyResult SetItem(PyCallContext context, PyDictObject self, PyObject key, PyObject value)
    {
        return self.SetItem(context, key, value);
    }

    protected override PyResult DelItem(PyCallContext context, PyDictObject self, PyObject key)
    {
        return self.DelItem(context, key);
    }

    protected override PyResult Contains(PyCallContext context, PyDictObject self, PyObject item)
    {
        var result = self.GetItem(context, item);
        if (result.IsSuccessful)
            return PyBoolObject.True;

        if (result.IsKeyError)
            return PyBoolObject.False;

        return result;
    }

    protected override PyResult Repr(PyCallContext context, PyDictObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected override PyResult Len(PyCallContext context, PyDictObject self)
    {
        return PyIntObject.FromInteger(self.Count);
    }

    [PyMethod("items")]
    [PyFunctionParameters()]
    private static PyResult Items(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return PyDictItemsObject.Items(self);
    }

    [PyMethod("keys")]
    [PyFunctionParameters()]
    private static PyResult Keys(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return PyDictItemsObject.Keys(self);
    }

    [PyMethod("values")]
    [PyFunctionParameters()]
    private static PyResult Values(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return PyDictItemsObject.Values(self);
    }

    [PyMethod("clear")]
    [PyFunctionParameters()]
    private static PyResult Clear(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        self.Clear();
        return PyNoneObject.None;
    }

    [PyMethod("get")]
    [PyFunctionParameters("key", "default=None", "/")]
    private static PyResult Get(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        var result = self.GetItem(context, arguments[0]);
        if (result.IsKeyError)
            return arguments[1];
        return result;
    }

    [PyMethod("setdefault")]
    [PyFunctionParameters("key", "default=None", "/")]
    private static PyResult SetDefault(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        var result = self.GetItem(context, arguments[0]);
        if (!result.IsKeyError)
            return result;

        var setResult = self.SetItem(context, arguments[0], arguments[1]);
        if (setResult.IsError)
            return setResult;

        return arguments[1];
    }

    [PyMethod("pop", Order = 1)]
    [PyFunctionParameters("key", "/")]
    private static PyResult Pop_1(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return self.Pop(context, arguments[0]);
    }

    [PyMethod("pop", Order = 2)]
    [PyFunctionParameters("key", "default", "/")]
    private static PyResult Pop_2(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        var value = self.Pop(context, arguments[0]);
        if (value.IsKeyError)
            return arguments[1];
        return value;
    }

    [PyMethod("popitem")]
    [PyFunctionParameters()]
    private static PyResult PopItem(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return self.PopItem();
    }

    [PyMethod("copy")]
    [PyFunctionParameters()]
    private static PyResult Copy(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        return new PyDictObject(self);
    }

    [PyMethod("update")]
    [PyFunctionParameters("iterable_or_mapping=None", "**kwargs")]
    private static PyResult UpdateImpl(PyCallContext context, PyDictObject self, PyArguments arguments)
    {
        if (arguments[0] is not PyNoneObject)
        {
            var result = self.Update(context, arguments[0]);
            if (result.IsError)
                return result;
        }

        foreach (var pair in arguments.ExtraKwargs)
            self.SetItem(pair.Key, pair.Value);

        return PyNoneObject.None;
    }

    [PyClassMethod("fromkeys")]
    [PyFunctionParameters("iterable", "value=None", "/")]
    private static PyResult FromKeysImpl(PyCallContext context, PyTypeObject cls, PyArguments arguments)
    {
        return PyDictObject.PyFromKeys(context, cls, arguments[0], arguments[1]);
    }

    [AIGenerated]
    protected override PyResult Iter(PyCallContext context, PyDictObject self)
    {
        return PyDictItemIteratorObject.Keys(PyDictItemsObject.Keys(self));
    }

    protected override PyResult Reversed(PyCallContext context, PyDictObject self)
    {
        return PyDictItemIteratorObject.ReversedKeys(PyDictItemsObject.Keys(self));
    }

    [AIGenerated]
    protected override PyResult Eq(PyCallContext context, PyDictObject self, PyObject other)
    {
        if (other is not PyDictObject otherDict)
            return base.Eq(context, self, other);

        if (self.Count != otherDict.Count)
            return PyBoolObject.False;

        foreach (var entry in self.Entries)
        {
            var otherItem = otherDict.GetItem(context, entry.Key);
            if (otherItem.IsError)
            {
                if (otherItem.IsKeyError)
                    return PyBoolObject.False;

                return otherItem;
            }

            var eq = PyComparer.Eq(context, entry.Value, otherItem.Value);
            if (eq.IsError)
                return eq;

            if (!eq.Value.BoolValue)
                return PyBoolObject.False;
        }

        return PyBoolObject.True;
    }
}

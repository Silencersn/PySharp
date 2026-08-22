using PySharp.Compilation.CodeAnalysis;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace PySharp.Modules.Builtins;

[PyType("dict")]
public sealed partial class PyDictObjectType : PyTypeObject<PyDictObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl_1), nameof(NewImpl_2))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("**kwargs")]
    private static PyResult NewImpl_1(PyCallContext context, PyArguments arguments)
    {
        return PyDictObject.CreateDict(context, arguments.ExtraKwargs
            .Select(pair => KeyValuePair.Create<PyObject, PyObject>(PyStrObject.FromString(pair.Key), pair.Value)));
    }

    [PyFunctionParameters("iterable_or_mapping", "/", "**kwargs")]
    private static PyResult NewImpl_2(PyCallContext context, PyArguments arguments)
    {
        var dict = PyUtils.ToDict(context, arguments[0]);
        if (dict.IsError)
            return dict;

        foreach (var kwarg in arguments.ExtraKwargs)
            dict.Value.SetItem(kwarg.Key, kwarg.Value);

        return dict;
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
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

    protected override PyResult Bool(PyCallContext context, PyDictObject self)
    {
        return PyBoolObject.FromBoolean(self.Count > 0);
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

using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Collections.Frozen;

namespace PySharp.PyModules.Builtins;

public partial class PyListObject : PyObject, IPyObjectRecursiveRepr
{
    internal readonly List<PyObject> _list;

    public override PyTypeObject DefaultPyType => PyListObjectType.Shared;

    public PyListObject()
    {
        _list = [];
    }
    public PyListObject(IEnumerable<PyObject> list)
    {
        _list = [.. list];
    }

    public static PyListObject CreateList(params IEnumerable<PyObject> objects)
    {
        return new PyListObject(objects);
    }

    protected internal override PyObject? GetItemImpl(PyObject item)
    {
        if (!PyInteropService.TryGetIndex(item, out int index))
            return null;

        if (!Utils.TryGetItem(_list, index, "list index out of range", out var result))
            return null;

        return result;
    }

    protected internal override PyObject? SetItemImpl(PyObject key, PyObject value)
    {
        if (!PyInteropService.TryGetIndex(key, out int index))
            return null;

        if (!Utils.TrySetItem(_list, index, value, "list index out of range"))
            return null;

        return PyNoneObject.None;
    }

    protected internal override PyObject? ContainsImpl(PyObject item)
    {
        return PyBoolObject.FromBoolean(_list.Contains(item));
    }

	protected internal override PyBoolObject BoolImpl()
    {
        return _list.Count > 0;
    }

    protected internal override PyObject? ReprImpl()
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(this);
    }

    PyObject? IPyObjectRecursiveRepr.RecursiveRepr(HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(this, _list, "[", "]", ids);
    }

    [PyFunctionArgsDef("x", "/")]
    internal PyNoneObject AppendImpl(PyArguments arguments)
    {
        PyAppend(arguments[0]);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("iterable", "/")]
    internal PyNoneObject? ExtendImpl(PyArguments arguments)
    {
        var items = Utils.EnumeratedIterable(arguments[0]);
        if (items is null)
            return null;

        PyExtend(items);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("i", "x", "/")]
    internal PyNoneObject? InsertImpl(PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out int index))
            return null;

        PyInsert(index, arguments[1]);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("x", "/")]
    internal PyObject? RemoveImpl(PyArguments arguments)
    {
        if (PyRemove(arguments[0]))
            return PyNoneObject.None;

        return PyVirtualMachine.RaiseValueError("list.remove(x): x not in list");
    }

    [PyFunctionArgsDef("i=-1", "/")]
    internal PyObject? PopImpl(PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out int index))
            return null;

        if (Utils.IsIndexOutOfRange(index, _list.Count))
            return PyVirtualMachine.RaiseIndexError("IndexError: pop index out of range");

        return PyPop(index);
    }

    [PyFunctionArgsDef()]
    internal PyNoneObject ClearImpl(PyArguments arguments)
    {
        PyClear();
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("x", "/")]
    internal PyObject? IndexImpl_1(PyArguments arguments)
    {
        var index = PyIndex(arguments[0]);
        if (index is -1)
        {
            if (!PyInteropService.TryGetRepr(arguments[0], out var s))
                return null;

            return PyVirtualMachine.RaiseValueError($"ValueError: {s} is not in list");
        }

        return PyIntObject.FromInteger(index);
    }

    [PyFunctionArgsDef("x", "start", "/")]
    internal PyObject? IndexImpl_2(PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[1], out int start))
            return null;

        var index = PyIndex(arguments[0], start);
        if (index is -1)
        {
            if (!PyInteropService.TryGetRepr(arguments[0], out var s))
                return null;

            return PyVirtualMachine.RaiseValueError($"ValueError: {s} is not in list");
        }

        return PyIntObject.FromInteger(index);
    }

    [PyFunctionArgsDef("x", "start", "end", "/")]
    internal PyObject? IndexImpl_3(PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[1], out int start))
            return null;

        if (!PyInteropService.TryGetIndex(arguments[2], out int end))
            return null;

        var index = PyIndex(arguments[0], start, end);
        if (index is -1)
        {
            if (!PyInteropService.TryGetRepr(arguments[0], out var s))
                return null;

            return PyVirtualMachine.RaiseValueError($"ValueError: {s} is not in list");
        }

        return PyIntObject.FromInteger(index);
    }

    [PyFunctionArgsDef("x", "/")]
    internal PyIntObject CountImpl(PyArguments arguments)
    {
        return PyIntObject.FromInteger(PyCount(arguments[0]));
    }

    [PyFunctionArgsDef("*", "key=None", "reverse=False")]
    internal PyNoneObject? SortImpl(PyArguments arguments)
    {
        var keySelector = arguments["key"];
        if (!PyInteropService.TryGetBool(arguments["reverse"], out var reverse))
            return null;

        if (keySelector is PyNoneObject)
        {
            PySort(reverse: reverse);
        }
        else
        {
            Dictionary<PyObject, PyObject> itemToKey = [];
            foreach (var item in _list)
            {
                var key = keySelector.Call([item], FrozenDictionary<string, PyObject>.Empty);
                if (key is null)
                    return null;
                itemToKey[item] = key;
            }
            PySort(item => itemToKey[item], reverse);
        }

        return PyNoneObject.None;
    }

    [PyFunctionArgsDef()]
    internal PyNoneObject ReverseImpl(PyArguments arguments)
    {
        PyReverse();
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef()]
    internal PyListObject CopyImpl(PyArguments arguments)
    {
        return PyCopy();
    }

    protected internal override PyObject? IterImpl()
    {
        return new PyListIteratorObject(this);
    }

    protected internal override PyObject? LenImpl()
    {
        return PyIntObject.FromInteger(_list.Count);
    }

    protected internal override PyObject? EqImpl(PyObject other)
    {
        if (other is not PyListObject otherList)
            return base.EqImpl(other);

        return PyBoolObject.FromBoolean(_list.SequenceEqual(otherList._list, PyObjectRuntimeEqualityComparer.Shared));
    }
}

public sealed class PyListObjectType : PyPrimitiveTypeObject<PyListObjectType, PyListObject>
{
    public PyListObjectType()
    {
        AppendMethodDescriptor<PyListObject>("append", nameof(PyListObject.AppendImpl));
        AppendMethodDescriptor<PyListObject>("extend", nameof(PyListObject.ExtendImpl));
        AppendMethodDescriptor<PyListObject>("insert", nameof(PyListObject.InsertImpl));
        AppendMethodDescriptor<PyListObject>("remove", nameof(PyListObject.RemoveImpl));
        AppendMethodDescriptor<PyListObject>("pop", nameof(PyListObject.PopImpl));
        AppendMethodDescriptor<PyListObject>("clear", nameof(PyListObject.ClearImpl));
        AppendMethodDescriptor<PyListObject>("index", nameof(PyListObject.IndexImpl_1), nameof(PyListObject.IndexImpl_2), nameof(PyListObject.IndexImpl_3));
        AppendMethodDescriptor<PyListObject>("count", nameof(PyListObject.CountImpl));
        AppendMethodDescriptor<PyListObject>("sort", nameof(PyListObject.SortImpl));
        AppendMethodDescriptor<PyListObject>("reverse", nameof(PyListObject.ReverseImpl));
        AppendMethodDescriptor<PyListObject>("copy", nameof(PyListObject.CopyImpl));
    }

    public override string Name => "list";

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("iterable=()", "/")]
    private static PyListObject? NewImpl(PyArguments arguments)
    {
        var list = Utils.EnumeratedIterable(arguments[0]);
        if (list is null)
            return null;

        return new PyListObject(list);
    }

    protected internal override PyObject? NewImpl(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(args, kwargs);
    }
}

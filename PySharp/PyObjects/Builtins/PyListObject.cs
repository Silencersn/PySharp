using PySharp.AstNodes;
using PySharp.PyRuntime;
using PySharp.PyRuntime.PyAttributes;
using System.Text;

namespace PySharp.PyObjects.Builtins;

public partial class PyListObject : PyObject, IPyObjectRecursiveRepr
{
    internal readonly List<PyObject> _list;

    public override PyTypeObject PyType => PyBuiltinTypes.List;

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

    public override PyObject? GetItem(PyObject item)
    {
        if (!PyInteropService.TryGetIndex(item, out var index))
            return null;

        if (!Utils.TryGetItem(_list, index, "list index out of range", out var result))
            return null;

        return result;
    }

    public override PyObject? SetItem(PyObject key, PyObject value)
    {
        if (!PyInteropService.TryGetIndex(key, out var index))
            return null;

        if (!Utils.TrySetItem(_list, index, value, "list index out of range"))
            return null;

        return PyNoneObject.None;
    }

    public override PyObject? Contains(PyObject item)
    {
        return PyBoolObject.FromBoolean(_list.Contains(item));
    }

    public override PyBoolObject Bool()
    {
        return _list.Count > 0;
    }

    public override PyObject? Repr()
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
        var items = Utils.EnumerabledIterable(arguments[0]);
        if (items is null)
            return null;

        PyExtend(items);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("i", "x", "/")]
    internal PyNoneObject? InsertImpl(PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out var index))
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
        if (!PyInteropService.TryGetIndex(arguments[0], out var index))
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
        if (!PyInteropService.TryGetIndex(arguments[1], out var start))
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
        if (!PyInteropService.TryGetIndex(arguments[1], out var start))
            return null;

        if (!PyInteropService.TryGetIndex(arguments[2], out var end))
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
                var key = keySelector.Call([item], (Dictionary<string, PyObject>)[]);
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

    public override PyObject? Iter()
    {
        return new PyListIteratorObject(this);
    }

    public override PyObject? Len()
    {
        return PyIntObject.FromInteger(_list.Count);
    }

    public override PyObject? Eq(PyObject other)
    {
        if (other is not PyListObject otherList)
            return base.Eq(other);

        return PyBoolObject.FromBoolean(_list.SequenceEqual(otherList._list, PyObjectRuntimeEqualityComparer.Shared));
    }
}

public sealed class PyListObjectType : PyTypeObject
{
    public PyListObjectType()
    {
        AppendSpecialMethodsAsDescriptors<PyListObject>();

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

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateCount(1, 0))
            return PyVirtualMachine.RaiseTypeError(null);

        var list = Utils.EnumerabledIterable(pack[0]);
        if (list is null)
            return null;

        return new PyListObject(list);
    }
}

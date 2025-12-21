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

    PyObject? IPyObjectRecursiveRepr.RecursiveRepr(HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(this, _list, "[", "]", ids);
    }
}

public sealed class PyListObjectType : PyTypeObject<PyListObjectType, PyListObject>
{
    public override string Name => "list";

    public PyListObjectType()
    {
        AppendMethodDescriptor("append", Append);
        AppendMethodDescriptor("extend", Extend);
        AppendMethodDescriptor("insert", Insert);
        AppendMethodDescriptor("remove", Remove);
        AppendMethodDescriptor("pop", Pop);
        AppendMethodDescriptor("clear", Clear);
        AppendMethodDescriptor("index", Index_1, Index_2, Index_3);
        AppendMethodDescriptor("count", Count);
        AppendMethodDescriptor("sort", Sort);
        AppendMethodDescriptor("reverse", Reverse);
        AppendMethodDescriptor("copy", Copy);
    }

    private static readonly PyBuiltinFunctionOrMethodObject2 _new = new(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("iterable=()", "/")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var list = Utils.EnumeratedIterable(arguments[0]);
        if (list is null)
            return PyResult.CaptureExceptionFromPVM();

        return new PyListObject(list);
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(args, kwargs);
        if (obj is null)
            return PyResult.CaptureExceptionFromPVM();

        obj._pyType = cls;
        return obj;
    }

    protected internal override PyResult GetItem(PyCallContext context, PyListObject self, PyObject item)
    {
        if (!PyInteropService.TryGetIndex(item, out int index))
            return PyResult.CaptureExceptionFromPVM();
        if (!Utils.TryGetItem(self._list, index, "list index out of range", out var result))
            return PyResult.CaptureExceptionFromPVM();
        return result;
    }

    protected internal override PyResult SetItem(PyCallContext context, PyListObject self, PyObject key, PyObject value)
    {
        if (!PyInteropService.TryGetIndex(key, out int index))
            return PyResult.CaptureExceptionFromPVM();
        if (!Utils.TrySetItem(self._list, index, value, "list index out of range"))
            return PyResult.CaptureExceptionFromPVM();
        return PyNoneObject.None;
    }

    protected internal override PyResult Contains(PyCallContext context, PyListObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self._list.Contains(item));
    }

    protected internal override PyResult Repr(PyCallContext context, PyListObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(self) ?? PyResult.CaptureExceptionFromPVM();
    }

    protected internal override PyResult Bool(PyCallContext context, PyListObject self)
    {
        return PyBoolObject.FromBoolean(self._list.Count > 0);
    }

    protected internal override PyResult Iter(PyCallContext context, PyListObject self)
    {
        return new PyListIteratorObject(self);
    }

    protected internal override PyResult Len(PyCallContext context, PyListObject self)
    {
        return PyIntObject.FromInteger(self._list.Count);
    }

    protected internal override PyResult Eq(PyCallContext context, PyListObject self, PyObject other)
    {
        if (other is not PyListObject otherList)
            return base.Eq(context, self, other);
        return PyBoolObject.FromBoolean(self._list.SequenceEqual(otherList._list, PyObjectRuntimeEqualityComparer.Shared));
    }

    [PyFunctionArgsDef("x", "/")]
    internal PyResult Append(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        self.PyAppend(arguments[0]);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("iterable", "/")]
    internal PyResult Extend(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var items = Utils.EnumeratedIterable(arguments[0]);
        if (items is null)
            return PyResult.CaptureExceptionFromPVM();
        self.PyExtend(items);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("i", "x", "/")]
    internal PyResult Insert(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out int index))
            return PyResult.CaptureExceptionFromPVM();
        self.PyInsert(index, arguments[1]);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("x", "/")]
    internal PyResult Remove(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        if (self.PyRemove(arguments[0]))
            return PyNoneObject.None;
        return PyResult.RaiseValueError("list.remove(x): x not in list");
    }

    [PyFunctionArgsDef("i=-1", "/")]
    internal PyResult Pop(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[0], out int index))
            return PyResult.CaptureExceptionFromPVM();
        if (Utils.IsIndexOutOfRange(index, self._list.Count))
            return PyResult.RaiseIndexError("IndexError: pop index out of range");
        return self.PyPop(index);
    }

    [PyFunctionArgsDef()]
    internal PyResult Clear(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        self.PyClear();
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("x", "/")]
    internal PyResult Index_1(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var index = self.PyIndex(arguments[0]);
        if (index is -1)
        {
            if (!PyInteropService.TryGetRepr(arguments[0], out var s))
                return PyResult.CaptureExceptionFromPVM();
            return PyResult.RaiseValueError($"ValueError: {s} is not in list");
        }
        return PyIntObject.FromInteger(index);
    }

    [PyFunctionArgsDef("x", "start", "/")]
    internal PyResult Index_2(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[1], out int start))
            return PyResult.CaptureExceptionFromPVM();
        var index = self.PyIndex(arguments[0], start);
        if (index is -1)
        {
            if (!PyInteropService.TryGetRepr(arguments[0], out var s))
                return PyResult.CaptureExceptionFromPVM();
            return PyResult.RaiseValueError($"ValueError: {s} is not in list");
        }
        return PyIntObject.FromInteger(index);
    }

    [PyFunctionArgsDef("x", "start", "end", "/")]
    internal PyResult Index_3(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        if (!PyInteropService.TryGetIndex(arguments[1], out int start))
            return PyResult.CaptureExceptionFromPVM();
        if (!PyInteropService.TryGetIndex(arguments[2], out int end))
            return PyResult.CaptureExceptionFromPVM();
        var index = self.PyIndex(arguments[0], start, end);
        if (index is -1)
        {
            if (!PyInteropService.TryGetRepr(arguments[0], out var s))
                return PyResult.CaptureExceptionFromPVM();
            return PyResult.RaiseValueError($"ValueError: {s} is not in list");
        }
        return PyIntObject.FromInteger(index);
    }

    [PyFunctionArgsDef("x", "/")]
    internal PyResult Count(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        return PyIntObject.FromInteger(self.PyCount(arguments[0]));
    }

    [PyFunctionArgsDef("*", "key=None", "reverse=False")]
    internal PyResult Sort(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var keySelector = arguments["key"];
        if (!PyInteropService.TryGetBool(arguments["reverse"], out var reverse))
            return PyResult.CaptureExceptionFromPVM();
        if (keySelector is PyNoneObject)
        {
            self.PySort(reverse: reverse);
        }
        else
        {
            Dictionary<PyObject, PyObject> itemToKey = [];
            foreach (var item in self._list)
            {
                var key = keySelector.Call([item], FrozenDictionary<string, PyObject>.Empty);
                if (key is null)
                    return PyResult.CaptureExceptionFromPVM();
                itemToKey[item] = key;
            }
            self.PySort(item => itemToKey[item], reverse);
        }
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef()]
    internal PyResult Reverse(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        self.PyReverse();
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef()]
    internal PyResult Copy(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        return self.PyCopy();
    }
}
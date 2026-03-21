using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System.Collections;
using System.Collections.Frozen;

namespace PySharp.Modules.Builtins;

public partial class PyListObject : PyObject, IPyObjectRecursiveRepr, IList<PyObject>, IReadOnlyList<PyObject>
{
    private readonly List<PyObject> _list;

    public override PyTypeObject DefaultPyType => PyListObjectType.Shared;

    internal List<PyObject> InternalList => _list;
    public int Count => _list.Count;

    bool ICollection<PyObject>.IsReadOnly => false;

    public PyObject this[int index]
    {
        get => _list[index];
        set => _list[index] = value;
    }

    private PyListObject()
    {
        _list = [];
    }
    private PyListObject(List<PyObject> list)
    {
        _list = list;
    }

    public static PyListObject CreateList(params IEnumerable<PyObject> objects)
    {
        return new PyListObject([.. objects]);
    }

    public static PyListObject CreateList(params ReadOnlySpan<PyObject> objects)
    {
        return new PyListObject([.. objects]);
    }

    public static PyListObject CreateProxy(List<PyObject> list)
    {
        return new PyListObject(list);
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(context, this, _list, "[", "]", ids);
    }

    public List<PyObject>.Enumerator GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    IEnumerator<PyObject> IEnumerable<PyObject>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_list).GetEnumerator();
    }

    public void Add(PyObject item)
    {
        _list.Add(item);
    }

    public void Clear()
    {
        _list.Clear();
    }

    public bool Contains(PyObject item)
    {
        return _list.Contains(item);
    }

    public void CopyTo(PyObject[] array, int arrayIndex)
    {
        _list.CopyTo(array, arrayIndex);
    }

    public bool Remove(PyObject item)
    {
        return _list.Remove(item);
    }

    public int IndexOf(PyObject item)
    {
        return _list.IndexOf(item);
    }

    public void Insert(int index, PyObject item)
    {
        _list.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        _list.RemoveAt(index);
    }
}

[PyType("list")]
public sealed partial class PyListObjectType : PyTypeObject<PyListObjectType, PyListObject>
{
    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("iterable=()", "/")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        return PyUtils.IterableToList(context, arguments[0]);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult GetItem(PyCallContext context, PyListObject self, PyObject item)
    {
        var result = PySpecialMethods.Index(context, item);
        if (result.IsError)
            return result;
        return Utils.GetListItem(self, result.Value.Int32Value, PySR.Runtime_List_IndexOutOfRange);
    }

    protected override PyResult SetItem(PyCallContext context, PyListObject self, PyObject key, PyObject value)
    {
        var result = PySpecialMethods.Index(context, key);
        if (result.IsError)
            return result;
        if (!Utils.TrySetListItem(self, result.Value.Int32Value, value))
            return PyResult.IndexError(PySR.Runtime_List_IndexOutOfRange);
        return PyNoneObject.None;
    }

    protected override PyResult Contains(PyCallContext context, PyListObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self.Contains(item));
    }

    protected override PyResult Repr(PyCallContext context, PyListObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected override PyResult Bool(PyCallContext context, PyListObject self)
    {
        return PyBoolObject.FromBoolean(self.Count > 0);
    }

    protected override PyResult Iter(PyCallContext context, PyListObject self)
    {
        return new PyListIteratorObject(self);
    }

    protected override PyResult Len(PyCallContext context, PyListObject self)
    {
        return PyIntObject.FromInteger(self.Count);
    }

    protected override PyResult Eq(PyCallContext context, PyListObject self, PyObject other)
    {
        if (other is not PyListObject otherList)
            return base.Eq(context, self, other);
        return PyBoolObject.FromBoolean(self.SequenceEqual(otherList, PyObjectComparer.Default));
    }

    [PyMethod("append")]
    [PyFunctionArgsDef("x", "/")]
    private static PyResult Append(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        self.PyAppend(arguments[0]);
        return PyNoneObject.None;
    }

    [PyMethod("extend")]
    [PyFunctionArgsDef("iterable", "/")]
    private static PyResult Extend(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        return self.PyExtend(context, arguments[0]);
    }

    [PyMethod("insert")]
    [PyFunctionArgsDef("i", "x", "/")]
    private static PyResult Insert(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;
        self.PyInsert(result.Value.Int32Value, arguments[1]);
        return PyNoneObject.None;
    }

    [PyMethod("remove")]
    [PyFunctionArgsDef("x", "/")]
    private static PyResult Remove(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        if (self.PyRemove(arguments[0]))
            return PyNoneObject.None;
        return PyResult.ValueError(PySR.Runtime_List_ItemNotFound, "remove");
    }

    [PyMethod("pop")]
    [PyFunctionArgsDef("i=-1", "/")]
    private static PyResult Pop(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;
        if (Utils.IsIndexOutOfRange(result.Value.Int32Value, self.Count))
            return PyResult.IndexError(PySR.Runtime_List_PopIndexOutOfRange);
        return self.PyPop(result.Value.Int32Value);
    }

    [PyMethod("clear")]
    [PyFunctionArgsDef()]
    private static PyResult Clear(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        self.PyClear();
        return PyNoneObject.None;
    }

    [PyMethod("index", Order = 1)]
    [PyFunctionArgsDef("x", "/")]
    private static PyResult Index_1(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var index = self.PyIndex(arguments[0]);
        if (index is -1)
        {
            var result = PySpecialMethods.Repr(context, arguments[0]);
            if (result.IsError)
                return result;

            return PyResult.ValueError(PySR.Runtime_List_ItemNotFound, "index");
        }
        return PyIntObject.FromInteger(index);
    }

    [PyMethod("index", Order = 2)]
    [PyFunctionArgsDef("x", "start", "/")]
    private static PyResult Index_2(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[1]);
        if (result.IsError)
            return result;
        var index = self.PyIndex(arguments[0], result.Value.Int32Value);
        if (index is -1)
        {
            var reprResult = PySpecialMethods.Repr(context, arguments[0]);
            if (reprResult.IsError)
                return reprResult;

            return PyResult.ValueError(PySR.Runtime_List_ItemNotFound, "index");
        }
        return PyIntObject.FromInteger(index);
    }

    [PyMethod("index", Order = 3)]
    [PyFunctionArgsDef("x", "start", "end", "/")]
    private static PyResult Index_3(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var startResult = PySpecialMethods.Index(context, arguments[1]);
        if (startResult.IsError)
            return startResult;
        var endResult = PySpecialMethods.Index(context, arguments[2]);
        if (endResult.IsError)
            return endResult;
        var index = self.PyIndex(arguments[0], startResult.Value.Int32Value, endResult.Value.Int32Value);
        if (index is -1)
        {
            var reprResult = PySpecialMethods.Repr(context, arguments[0]);
            if (reprResult.IsError)
                return reprResult;

            return PyResult.ValueError(PySR.Runtime_List_ItemNotFound, "index");
        }
        return PyIntObject.FromInteger(index);
    }

    [PyMethod("count")]
    [PyFunctionArgsDef("x", "/")]
    private static PyResult Count(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        return PyIntObject.FromInteger(self.PyCount(arguments[0]));
    }

    [PyMethod("sort")]
    [PyFunctionArgsDef("*", "key=None", "reverse=False")]
    private static PyResult Sort(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        return self.PySort(context, arguments["key"], arguments["reverse"]);
    }

    [PyMethod("reverse")]
    [PyFunctionArgsDef()]
    private static PyResult Reverse(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        self.PyReverse();
        return PyNoneObject.None;
    }

    [PyMethod("copy")]
    [PyFunctionArgsDef()]
    private static PyResult Copy(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        return self.PyCopy();
    }
}
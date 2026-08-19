using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System.Collections;
using System.Runtime.InteropServices;

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

    internal static PyListObject CreateProxy(List<PyObject> list)
    {
        return new PyListObject(list);
    }

    internal Span<PyObject> AsSpan()
    {
        return CollectionsMarshal.AsSpan(_list);
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<PyObject> ids)
    {
        return Utils.CollectionRecursiveRepr(context, this, _list, "[", "]", ids);
    }

    public List<PyObject>.Enumerator GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    IEnumerator<PyObject> IEnumerable<PyObject>.GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Add(PyObject item)
    {
        _list.Add(item);
    }

    public void Clear()
    {
        _list.Clear();
    }

    bool ICollection<PyObject>.Contains(PyObject item)
    {
        throw PySharpNotSupportedException.ContextNeeded();
    }

    public void CopyTo(PyObject[] array, int arrayIndex)
    {
        _list.CopyTo(array, arrayIndex);
    }

    bool ICollection<PyObject>.Remove(PyObject item)
    {
        throw PySharpNotSupportedException.ContextNeeded();
    }

    int IList<PyObject>.IndexOf(PyObject item)
    {
        throw PySharpNotSupportedException.ContextNeeded();
    }

    public void Insert(int index, PyObject item)
    {
        _list.Insert(index, item);
    }

    void IList<PyObject>.RemoveAt(int index)
    {
        throw PySharpNotSupportedException.ContextNeeded();
    }
}

[PyType("list")]
public sealed partial class PyListObjectType : PyTypeObject<PyListObject>
{
    [PyExport(PySpecialNames.New, nameof(NewImpl))]
    private static partial PyBuiltinFunctionOrMethodObject _new { get; }

    [PyFunctionParameters("iterable=()", "/")]
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
        return self.PyGetItem(context, item);
    }

    protected override PyResult SetItem(PyCallContext context, PyListObject self, PyObject key, PyObject value)
    {
        return self.PySetItem(context, key, value);
    }

    protected override PyResult DelItem(PyCallContext context, PyListObject self, PyObject key)
    {
        return self.PyDelItem(context, key);
    }

    protected override PyResult Contains(PyCallContext context, PyListObject self, PyObject item)
    {
        foreach (var element in self.AsSpan())
        {
            var eq = PyComparer.Eq(context, element, item);
            if (eq.IsError)
                return eq.ExceptionResult;

            if (eq.Value.BoolValue)
                return PyBoolObject.True;
        }
        return PyBoolObject.False;
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
            return PyNotImplementedObject.NotImplemented;
        return PyCollectionComparer.Eq(context, self.AsSpan(), otherList.AsSpan());
    }

    protected override PyResult Lt(PyCallContext context, PyListObject self, PyObject other)
    {
        if (other is not PyListObject otherList)
            return PyNotImplementedObject.NotImplemented;
        return PyCollectionComparer.Lt(context, self.AsSpan(), otherList.AsSpan());
    }

    protected override PyResult Le(PyCallContext context, PyListObject self, PyObject other)
    {
        if (other is not PyListObject otherList)
            return PyNotImplementedObject.NotImplemented;
        return PyCollectionComparer.Le(context, self.AsSpan(), otherList.AsSpan());
    }

    protected override PyResult Gt(PyCallContext context, PyListObject self, PyObject other)
    {
        if (other is not PyListObject otherList)
            return PyNotImplementedObject.NotImplemented;
        return PyCollectionComparer.Gt(context, self.AsSpan(), otherList.AsSpan());
    }

    protected override PyResult Ge(PyCallContext context, PyListObject self, PyObject other)
    {
        if (other is not PyListObject otherList)
            return PyNotImplementedObject.NotImplemented;
        return PyCollectionComparer.Ge(context, self.AsSpan(), otherList.AsSpan());
    }

    protected override PyResult Add(PyCallContext context, PyListObject self, PyObject other)
    {
        if (other is not PyListObject)
            return PyNotImplementedObject.NotImplemented;
        return self.PyAdd(other);
    }

    protected override PyResult IAdd(PyCallContext context, PyListObject self, PyObject other)
    {
        var result = self.PyExtend(context, other);
        if (result.IsError)
            return result;
        return self;
    }

    protected override PyResult Mul(PyCallContext context, PyListObject self, PyObject other)
    {
        var result = PySpecialMethods.Index(context, other);
        if (result.IsError)
            return result;
        return self.PyMul(result.Value.Int32Value);
    }

    protected override PyResult RMul(PyCallContext context, PyListObject self, PyObject other)
    {
        return Mul(context, self, other);
    }

    protected override PyResult IMul(PyCallContext context, PyListObject self, PyObject other)
    {
        var result = PySpecialMethods.Index(context, other);
        if (result.IsError)
            return result;
        return self.PyIMul(result.Value.Int32Value);
    }

    [PyMethod("append")]
    [PyFunctionParameters("x", "/")]
    private static PyResult Append(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        self.PyAppend(arguments[0]);
        return PyNoneObject.None;
    }

    [PyMethod("extend")]
    [PyFunctionParameters("iterable", "/")]
    private static PyResult Extend(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        return self.PyExtend(context, arguments[0]);
    }

    [PyMethod("insert")]
    [PyFunctionParameters("i", "x", "/")]
    private static PyResult Insert(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;
        self.PyInsert(result.Value.Int32Value, arguments[1]);
        return PyNoneObject.None;
    }

    [PyMethod("remove")]
    [PyFunctionParameters("x", "/")]
    private static PyResult Remove(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        if (self.PyRemove(context, arguments[0]))
            return PyNoneObject.None;
        return PyResult.ValueError(PySR.Runtime_List_ItemNotFound, "remove");
    }

    [PyMethod("pop")]
    [PyFunctionParameters("i=-1", "/")]
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
    [PyFunctionParameters()]
    private static PyResult Clear(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        self.Clear();
        return PyNoneObject.None;
    }

    [PyMethod("index", Order = 1)]
    [PyFunctionParameters("x", "/")]
    private static PyResult Index_1(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var index = self.PyIndex(context, arguments[0]);
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
    [PyFunctionParameters("x", "start", "/")]
    private static PyResult Index_2(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[1]);
        if (result.IsError)
            return result;
        var index = self.PyIndex(context, arguments[0], result.Value.Int32Value);
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
    [PyFunctionParameters("x", "start", "end", "/")]
    private static PyResult Index_3(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var startResult = PySpecialMethods.Index(context, arguments[1]);
        if (startResult.IsError)
            return startResult;
        var endResult = PySpecialMethods.Index(context, arguments[2]);
        if (endResult.IsError)
            return endResult;
        var index = self.PyIndex(context, arguments[0], startResult.Value.Int32Value, endResult.Value.Int32Value);
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
    [PyFunctionParameters("x", "/")]
    private static PyResult Count(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        return PyIntObject.FromInteger(self.PyCount(context, arguments[0]));
    }

    [PyMethod("sort")]
    [PyFunctionParameters("*", "key=None", "reverse=False")]
    private static PyResult Sort(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        return self.PySort(context, arguments.GetKwargByIndex(0), arguments.GetKwargByIndex(1));
    }

    [PyMethod("reverse")]
    [PyFunctionParameters()]
    private static PyResult Reverse(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        self.PyReverse();
        return PyNoneObject.None;
    }

    [PyMethod("copy")]
    [PyFunctionParameters()]
    private static PyResult Copy(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        return self.PyCopy();
    }
}

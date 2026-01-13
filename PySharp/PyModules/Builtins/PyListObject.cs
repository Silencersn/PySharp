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

    PyResult IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(context, this, _list, "[", "]", ids);
    }
}

public sealed class PyListObjectType : PyTypeObject<PyListObjectType, PyListObject>
{
    public override string Module => "builtins";
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

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("iterable=()", "/")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        if (!Utils.TryEnumeratedIterable(context, arguments[0], out var list, out var err))
            return err.Value;

        return new PyListObject(list);
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
        return Utils.GetListItem(self._list, result.Value.Int32Value, "list index out of range");
    }

    protected override PyResult SetItem(PyCallContext context, PyListObject self, PyObject key, PyObject value)
    {
        var result = PySpecialMethods.Index(context, key);
        if (result.IsError)
            return result;
        if (!Utils.TrySetListItem(self._list, result.Value.Int32Value, value))
            return PyResult.RaiseIndexError("list index out of range");
        return PyNoneObject.None;
    }

    protected override PyResult Contains(PyCallContext context, PyListObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self._list.Contains(item));
    }

    protected override PyResult Repr(PyCallContext context, PyListObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected override PyResult Bool(PyCallContext context, PyListObject self)
    {
        return PyBoolObject.FromBoolean(self._list.Count > 0);
    }

    protected override PyResult Iter(PyCallContext context, PyListObject self)
    {
        return new PyListIteratorObject(self);
    }

    protected override PyResult Len(PyCallContext context, PyListObject self)
    {
        return PyIntObject.FromInteger(self._list.Count);
    }

    protected override PyResult Eq(PyCallContext context, PyListObject self, PyObject other)
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
        if (!Utils.TryEnumeratedIterable(context, arguments[0], out var items, out var err))
            return err.Value;
        self.PyExtend(items);
        return PyNoneObject.None;
    }

    [PyFunctionArgsDef("i", "x", "/")]
    internal PyResult Insert(PyCallContext context, PyListObject self, PyArguments arguments)
    {
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;
        self.PyInsert(result.Value.Int32Value, arguments[1]);
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
        var result = PySpecialMethods.Index(context, arguments[0]);
        if (result.IsError)
            return result;
        if (Utils.IsIndexOutOfRange(result.Value.Int32Value, self._list.Count))
            return PyResult.RaiseIndexError("IndexError: pop index out of range");
        return self.PyPop(result.Value.Int32Value);
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
            var result = PySpecialMethods.Repr(context, arguments[0]);
            if (result.IsError)
                return result;

            return PyResult.RaiseValueError($"ValueError: {result.Value.Value} is not in list");
        }
        return PyIntObject.FromInteger(index);
    }

    [PyFunctionArgsDef("x", "start", "/")]
    internal PyResult Index_2(PyCallContext context, PyListObject self, PyArguments arguments)
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

            return PyResult.RaiseValueError($"ValueError: {reprResult.Value.Value} is not in list");
        }
        return PyIntObject.FromInteger(index);
    }

    [PyFunctionArgsDef("x", "start", "end", "/")]
    internal PyResult Index_3(PyCallContext context, PyListObject self, PyArguments arguments)
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

            return PyResult.RaiseValueError($"ValueError: {reprResult.Value.Value} is not in list");
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
        var result = PySpecialMethods.Bool(context, arguments["reverse"]);
        if (result.IsError)
            return result;
        if (keySelector is PyNoneObject)
        {
            self.PySort(reverse: result.Value.BoolValue);
        }
        else
        {
            Dictionary<PyObject, PyObject> itemToKey = [];
            foreach (var item in self._list)
            {
                var key = keySelector.Call(context, [item], FrozenDictionary<string, PyObject>.Empty);
                if (key.IsError)
                    return key;
                itemToKey[item] = key.Value;
            }
            self.PySort(item => itemToKey[item], result.Value.BoolValue);
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
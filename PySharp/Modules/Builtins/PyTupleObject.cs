using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.PyAttributes;
using System.Collections;

namespace PySharp.Modules.Builtins;

public partial class PyTupleObject : PyObject, IPyObjectRecursiveRepr, IReadOnlyList<PyObject>
{
    private readonly PyObject[] _array;

    internal PyObject[] InternalArray => _array;

    public override PyTypeObject DefaultPyType => PyTupleObjectType.Shared;
    public static PyTupleObject Empty { get; } = new([]);

    public int Count => _array.Length;

    public PyObject this[int index] => _array[index];

    private PyTupleObject(PyObject[] array)
    {
        _array = array;
    }

    public static PyTupleObject CreateTuple(params IEnumerable<PyObject> items)
    {
        if (items.TryGetNonEnumeratedCount(out var count) && count is 0)
            return Empty;

        var array = items.ToArray();
        if (array.Length is 0)
            return Empty;

        return new PyTupleObject(array);
    }
    public static PyTupleObject CreateTuple(params ReadOnlySpan<PyObject> items)
    {
        if (items.IsEmpty)
            return Empty;

        return new PyTupleObject(items.ToArray());
    }

    public static PyTupleObject CreateProxy(PyObject[] array)
    {
        ArgumentNullException.ThrowIfNull(array);
        return new PyTupleObject(array);
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<PyObject> ids)
    {
        return PyUtils.CollectionRecursiveRepr(context, this, _array, "(", ")", ids, forceTrailingComma: true);
    }

    public IEnumerator<PyObject> GetEnumerator()
    {
        return ((IEnumerable<PyObject>)_array).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _array.GetEnumerator();
    }

    public ReadOnlySpan<PyObject> AsSpan()
    {
        return _array.AsSpan();
    }
}

[PyType("tuple")]
public sealed partial class PyTupleObjectType : PyTypeObject<PyTupleObject>
{

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateSinglePositionalArg(args, kwargs, out var err))
            return err.Value;

        var tuple = PyUtils.IterableToTuple(context, args[0]);
        if (tuple.IsError)
            return tuple;

        var obj = tuple.Value;
        obj._pyType = cls;
        return obj;
    }

    protected override PyResult Iter(PyCallContext context, PyTupleObject self)
    {
        return new PyTupleIteratorObject(self);
    }

    [AIGenerated]
    protected override PyResult GetItem(PyCallContext context, PyTupleObject self, PyObject item)
    {
        return self.PyGetItem(context, item);
    }

    protected override PyResult Contains(PyCallContext context, PyTupleObject self, PyObject item)
    {
        return PyUtils.Contains(context, self.AsSpan(), item);
    }

    protected override PyResult Bool(PyCallContext context, PyTupleObject self)
    {
        return PyBoolObject.FromBoolean(self.Count > 0);
    }

    protected override PyResult Repr(PyCallContext context, PyTupleObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected override PyResult Len(PyCallContext context, PyTupleObject self)
    {
        return PyIntObject.FromInteger(self.Count);
    }

    protected override PyResult Eq(PyCallContext context, PyTupleObject self, PyObject other)
    {
        if (other is not PyTupleObject otherTuple)
            return PyNotImplementedObject.NotImplemented;
        return PyCollectionComparer.Eq(context, self.AsSpan(), otherTuple.AsSpan());
    }

    [AIGenerated]
    protected override PyResult Lt(PyCallContext context, PyTupleObject self, PyObject other)
    {
        if (other is not PyTupleObject otherTuple)
            return PyNotImplementedObject.NotImplemented;
        return PyCollectionComparer.Lt(context, self.AsSpan(), otherTuple.AsSpan());
    }

    [AIGenerated]
    protected override PyResult Le(PyCallContext context, PyTupleObject self, PyObject other)
    {
        if (other is not PyTupleObject otherTuple)
            return PyNotImplementedObject.NotImplemented;
        return PyCollectionComparer.Le(context, self.AsSpan(), otherTuple.AsSpan());
    }

    [AIGenerated]
    protected override PyResult Gt(PyCallContext context, PyTupleObject self, PyObject other)
    {
        if (other is not PyTupleObject otherTuple)
            return PyNotImplementedObject.NotImplemented;
        return PyCollectionComparer.Gt(context, self.AsSpan(), otherTuple.AsSpan());
    }

    [AIGenerated]
    protected override PyResult Ge(PyCallContext context, PyTupleObject self, PyObject other)
    {
        if (other is not PyTupleObject otherTuple)
            return PyNotImplementedObject.NotImplemented;
        return PyCollectionComparer.Ge(context, self.AsSpan(), otherTuple.AsSpan());
    }

    [AIGenerated]
    protected override PyResult Hash(PyCallContext context, PyTupleObject self)
    {
        return self.PyHash(context);
    }

    [AIGenerated]
    protected override PyResult Add(PyCallContext context, PyTupleObject self, PyObject other)
    {
        return self.PyAdd(other);
    }

    [AIGenerated]
    protected override PyResult Mul(PyCallContext context, PyTupleObject self, PyObject other)
    {
        var result = PySpecialMethods.Index(context, other);
        if (result.IsError)
            return result;
        return self.PyMul(result.Value.Int32Value);
    }

    [AIGenerated]
    protected override PyResult RMul(PyCallContext context, PyTupleObject self, PyObject other)
    {
        return Mul(context, self, other);
    }

    [PyMethod("index", Order = 1)]
    [PyFunctionParameters("x", "/")]
    [AIGenerated]
    private static PyResult Index_1(PyCallContext context, PyTupleObject self, PyArguments arguments)
    {
        var index = self.PyIndex(context, arguments[0]);
        if (index is -1)
            return PyResult.ValueError(PySR.Runtime_Tuple_ItemNotFound, "index");
        return PyIntObject.FromInteger(index);
    }

    [PyMethod("index", Order = 2)]
    [PyFunctionParameters("x", "start", "/")]
    [AIGenerated]
    private static PyResult Index_2(PyCallContext context, PyTupleObject self, PyArguments arguments)
    {
        var startResult = PySpecialMethods.Index(context, arguments[1]);
        if (startResult.IsError)
            return startResult;

        var index = self.PyIndex(context, arguments[0], startResult.Value.Int32Value);
        if (index is -1)
            return PyResult.ValueError(PySR.Runtime_Tuple_ItemNotFound, "index");
        return PyIntObject.FromInteger(index);
    }

    [PyMethod("index", Order = 3)]
    [PyFunctionParameters("x", "start", "end", "/")]
    [AIGenerated]
    private static PyResult Index_3(PyCallContext context, PyTupleObject self, PyArguments arguments)
    {
        var startResult = PySpecialMethods.Index(context, arguments[1]);
        if (startResult.IsError)
            return startResult;
        var endResult = PySpecialMethods.Index(context, arguments[2]);
        if (endResult.IsError)
            return endResult;

        var index = self.PyIndex(context, arguments[0], startResult.Value.Int32Value, endResult.Value.Int32Value);
        if (index is -1)
            return PyResult.ValueError(PySR.Runtime_Tuple_ItemNotFound, "index");
        return PyIntObject.FromInteger(index);
    }

    [PyMethod("count")]
    [PyFunctionParameters("x", "/")]
    [AIGenerated]
    private static PyResult Count(PyCallContext context, PyTupleObject self, PyArguments arguments)
    {
        return PyIntObject.FromInteger(self.PyCount(context, arguments[0]));
    }
}

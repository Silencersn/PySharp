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

    /// <summary>
    /// Always a fresh instance, even for no items — for callers that retag
    /// the result to another type (subclass construction);
    /// <see cref="CreateTuple"/> hands out the shared empty singleton.
    /// </summary>
    public static PyTupleObject CreateTupleNoCache(ReadOnlySpan<PyObject> items)
    {
        return new PyTupleObject(items.ToArray());
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
        return new ReadOnlySpan<PyObject>(_array);
    }
}

[PyType("tuple")]
public sealed partial class PyTupleObjectType : PyTypeObject<PyTupleObject>
{

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // CPython tuple_new: no keyword parameters at all and at most one
        // positional argument; the empty tuple needs no conversion
        if (kwargs.Count > 0)
            return PyResult.TypeError(PySR.Runtime_Tuple_TakesNoKwargs);
        if (args.Count > 1)
            return PyResult.TypeError(PySR.Runtime_Tuple_ExpectedAtMostOne, args.Count);

        PyTupleObject tuple;
        if (args.Count is 0)
        {
            tuple = PyTupleObject.Empty;
        }
        else if (ReferenceEquals(cls, PyTupleObjectType.Shared) &&
                 args[0] is PyTupleObject exactSource &&
                 args[0].PyType == PyTupleObjectType.Shared)
        {
            // CPython tuple_new: an exact tuple source returns itself
            return exactSource;
        }
        else
        {
            var result = PyUtils.IterableToTuple(context, args[0]);
            if (result.IsError)
                return result;
            tuple = result.Value;
        }

        // CPython tuple_new hands subtypes a fresh copy (tuple_subtype_new):
        // the empty tuple is a shared singleton, so retagging it in place
        // would retype () everywhere
        if (tuple.PyType != cls)
        {
            tuple = PyTupleObject.CreateTupleNoCache(tuple.AsSpan());
            tuple._pyType = cls;
        }
        return tuple;
    }

    protected override PyResult Iter(PyCallContext context, PyTupleObject self)
    {
        return new PyTupleIteratorObject(self);
    }

    [AIGenerated]
    protected override PyResult GetItem(PyCallContext context, PyTupleObject self, PyObject item)
    {
        return PyUtils.GetSequenceItem(context, self.AsSpan(), item, PyTupleObject.CreateTuple, PySR.Runtime_Tuple_IndexOutOfRange);
    }

    protected override PyResult Contains(PyCallContext context, PyTupleObject self, PyObject item)
    {
        return PyUtils.Contains(context, self.AsSpan(), item);
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

    // CPython's reflected wrappers cover as_number and sq_repeat slots
    // only; sq_concat has no reflected variant (tuple.__radd__ does not
    // exist). __mul__ keeps its __rmul__ entry with the non-swapping
    // self*value order of wrap_indexargfunc.
    protected override bool SynthesizeReflectedAdd => false;
    protected override bool ReflectedMulSwapsOperands => false;

    [AIGenerated]
    protected override PyResult Add(PyCallContext context, PyTupleObject self, PyObject other)
    {
        return self.PyAdd(context, other);
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

        var index = self.PyIndex(context, arguments[0], PyUtils.SaturateIndex(startResult.Value.Value));
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

        var index = self.PyIndex(context, arguments[0], PyUtils.SaturateIndex(startResult.Value.Value), PyUtils.SaturateIndex(endResult.Value.Value));
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

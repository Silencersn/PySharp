using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using System.Collections;

namespace PySharp.Modules.Builtins;

public class PyTupleObject : PyObject, IPyObjectRecursiveRepr, IReadOnlyList<PyObject>
{
    private readonly PyObject[] _array;

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

    public static PyTupleObject CreateProxy(PyObject[] array)
    {
        ArgumentNullException.ThrowIfNull(array);
        return new PyTupleObject(array);
    }

    PyResult<PyStrObject> IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(context, this, _array, "(", ")", ids, forceTrailingComma: true);
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

public sealed class PyTupleObjectType : PyTypeObject<PyTupleObjectType, PyTupleObject>
{
    public override string Module => "builtins";
    public override string Name => "tuple";

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

    protected override PyResult GetItem(PyCallContext context, PyTupleObject self, PyObject item)
    {
        var result = PySpecialMethods.Index(context, item);
        if (result.IsError)
            return result;
        return Utils.GetListItem(self, result.Value.Int32Value, "IndexError: tuple index out of range");
    }

    protected override PyResult Contains(PyCallContext context, PyTupleObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self.Contains(item));
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
            return base.Eq(context, self, other);
        return PyBoolObject.FromBoolean(self.SequenceEqual(otherTuple, PyObjectComparer.Default));
    }
}

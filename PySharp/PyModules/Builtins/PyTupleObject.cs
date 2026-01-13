using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyTupleObject : PyObject, IPyObjectRecursiveRepr
{
    internal readonly PyObject[] _array;

    public override PyTypeObject DefaultPyType => PyTupleObjectType.Shared;
    public static PyTupleObject Empty { get; } = new([]);

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

    PyResult IPyObjectRecursiveRepr.RecursiveRepr(PyCallContext context, HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(context, this, _array, "(", ")", ids, forceTrailingComma: true);
    }
}

public sealed class PyTupleObjectType : PyTypeObject<PyTupleObjectType, PyTupleObject>
{
    public override string Name => "tuple";

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!PyArgsValidator.ValidateSinglePositionalArg(args, kwargs, out var err))
            return err.Value;

        if (!Utils.TryEnumeratedIterable(context, args[0], out var tuple, out err))
            return err.Value;

        var obj = PyTupleObject.CreateTuple(tuple);
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
        return Utils.GetListItem(self._array, result.Value.Int32Value, "IndexError: tuple index out of range");
    }

    protected override PyResult Contains(PyCallContext context, PyTupleObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self._array.Contains(item));
    }

    protected override PyResult Bool(PyCallContext context, PyTupleObject self)
    {
        return PyBoolObject.FromBoolean(self._array.Length > 0);
    }

    protected override PyResult Repr(PyCallContext context, PyTupleObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected override PyResult Len(PyCallContext context, PyTupleObject self)
    {
        return PyIntObject.FromInteger(self._array.Length);
    }

    protected override PyResult Eq(PyCallContext context, PyTupleObject self, PyObject other)
    {
        if (other is not PyTupleObject otherTuple)
            return base.Eq(context, self, other);
        return PyBoolObject.FromBoolean(self._array.SequenceEqual(otherTuple._array, PyObjectRuntimeEqualityComparer.Shared));
    }
}

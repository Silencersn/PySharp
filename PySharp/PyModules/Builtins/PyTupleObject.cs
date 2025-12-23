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
        return Utils.CollectionRecursiveRepr(context, this, _array, "(", ")", ids);
    }
}

public sealed class PyTupleObjectType : PyTypeObject<PyTupleObjectType, PyTupleObject>
{
    public override string Name => "tuple";

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateCount(1, 0))
            return PyResult.RaiseTypeError(null);

        var tuple = Utils.EnumeratedIterable(pack[0]);
        if (tuple is null)
            return PyResult.CaptureExceptionFromPVM();

        var obj = PyTupleObject.CreateTuple(tuple);
        obj._pyType = cls;
        return obj;
    }

    protected internal override PyResult Iter(PyCallContext context, PyTupleObject self)
    {
        return new PyTupleIteratorObject(self);
    }

    protected internal override PyResult GetItem(PyCallContext context, PyTupleObject self, PyObject item)
    {
        if (!PySpecialMethods.TryGetIndex(context, item, out var index, out var result))
            return result;
        if (!Utils.TryGetItem(self._array, index.Int32Value, "IndexError: tuple index out of range", out var itemResult))
            return result;
        return itemResult;
    }

    protected internal override PyResult Contains(PyCallContext context, PyTupleObject self, PyObject item)
    {
        return PyBoolObject.FromBoolean(self._array.Contains(item));
    }

    protected internal override PyResult Bool(PyCallContext context, PyTupleObject self)
    {
        return PyBoolObject.FromBoolean(self._array.Length > 0);
    }

    protected internal override PyResult Repr(PyCallContext context, PyTupleObject self)
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(context, self);
    }

    protected internal override PyResult Len(PyCallContext context, PyTupleObject self)
    {
        return PyIntObject.FromInteger(self._array.Length);
    }
}

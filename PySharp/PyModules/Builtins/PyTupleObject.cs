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

    protected internal override PyObject? IterImpl()
    {
        return new PyTupleIteratorObject(this);
    }

    protected internal override PyObject? GetItemImpl(PyObject item)
    {
        if (!PyInteropService.TryGetIndex(item, out int index))
            return null;

        if (!Utils.TryGetItem(_array, index, "IndexError: tuple index out of range", out var result))
            return null;

        return result;
    }

    protected internal override PyBoolObject ContainsImpl(PyObject item)
    {
        return PyBoolObject.FromBoolean(_array.Contains(item));
    }

    protected internal override PyBoolObject BoolImpl()
    {
        return _array.Length > 0;
    }

    protected internal override PyObject? ReprImpl()
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(this);
    }

    protected internal override PyIntObject LenImpl()
    {
        return PyIntObject.FromInteger(_array.Length);
    }

    PyObject? IPyObjectRecursiveRepr.RecursiveRepr(HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(this, _array, "(", ")", ids);
    }
}

public sealed class PyTupleObjectType : PyPrimitiveTypeObject<PyTupleObjectType, PyTupleObject>
{
    public override string Name => "tuple";

    protected internal override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateCount(1, 0))
            return PyVirtualMachine.RaiseTypeError(null);

        var tuple = Utils.EnumeratedIterable(pack[0]);
        if (tuple is null)
            return null;

        return PyTupleObject.CreateTuple(tuple);
    }
}

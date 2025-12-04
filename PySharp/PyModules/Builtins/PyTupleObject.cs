using PySharp.PyRuntime;
using System.Text;

namespace PySharp.PyModules.Builtins;

public class PyTupleObject : PyObject, IPyObjectRecursiveRepr
{
    internal readonly PyObject[] _array;

    public override PyTypeObject PyType => PyBuiltinTypes.Tuple;

    public PyTupleObject()
    {
        _array = [];
    }
    public PyTupleObject(IEnumerable<PyObject> list)
    {
        _array = [.. list];
    }

    public static PyTupleObject CreateTuple(params IEnumerable<PyObject> items)
    {
        return new PyTupleObject(items);
    }

    public override PyObject? Iter()
    {
        return new PyTupleIteratorObject(this);
    }

    public override PyObject? GetItem(PyObject item)
    {
        if (!PyInteropService.TryGetIndex(item, out var index))
            return null;

        if (!Utils.TryGetItem(_array, index, "IndexError: tuple index out of range", out var result))
            return null;

        return result;
    }

    public override PyBoolObject Contains(PyObject item)
    {
        return PyBoolObject.FromBoolean(_array.Contains(item));
    }

    public override PyBoolObject Bool()
    {
        return _array.Length > 0;
    }

    public override PyObject? Repr()
    {
        return IPyObjectRecursiveRepr.RecursiveRepr(this);
    }

    public override PyIntObject Len()
    {
        return PyIntObject.FromInteger(_array.Length);
    }

    PyObject? IPyObjectRecursiveRepr.RecursiveRepr(HashSet<int> ids)
    {
        return Utils.CollectionRecursiveRepr(this, _array, "(", ")", ids);
    }
}

public sealed class PyTupleObjectType : PyTypeObject
{
    public override string Name => "tuple";

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateCount(1, 0))
            return PyVirtualMachine.RaiseTypeError(null);

        var tuple = Utils.EnumerabledIterable(pack[0]);
        if (tuple is null)
            return null;

        return new PyTupleObject(tuple);
    }
}

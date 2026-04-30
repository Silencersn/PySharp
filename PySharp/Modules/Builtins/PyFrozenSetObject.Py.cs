using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;

namespace PySharp.Modules.Builtins;

partial class PyFrozenSetObject
{
    internal PyResult PyDifference(PyCallContext context, IReadOnlyList<PyObject> others)
    {
        var result = new HashSet<PyObject>(_set, PyObjectComparer.Default);
        foreach (var other in others)
        {
            var iterable = PyUtils.IterableToList(context, other);
            if (iterable.IsError)
                return iterable;
            result.ExceptWith(iterable.Value);
        }
        return new PyFrozenSetObject(result);
    }

    internal PyResult PyIntersection(PyCallContext context, IReadOnlyList<PyObject> others)
    {
        var result = new HashSet<PyObject>(_set, PyObjectComparer.Default);
        foreach (var other in others)
        {
            var iterable = PyUtils.IterableToList(context, other);
            if (iterable.IsError)
                return iterable;
            result.IntersectWith(iterable.Value);
        }
        return new PyFrozenSetObject(result);
    }

    internal PyResult PyIsDisjoint(PyCallContext context, PyObject other)
    {
        var iterable = PyUtils.IterableToList(context, other);
        if (iterable.IsError)
            return iterable;

        return PyBoolObject.FromBoolean(!_set.Overlaps(iterable.Value));
    }

    internal PyResult PyIsSubset(PyCallContext context, PyObject other)
    {
        var iterable = PyUtils.IterableToList(context, other);
        if (iterable.IsError)
            return iterable;

        return PyBoolObject.FromBoolean(_set.IsSubsetOf(iterable.Value));
    }

    internal PyResult PyIsSuperset(PyCallContext context, PyObject other)
    {
        var iterable = PyUtils.IterableToList(context, other);
        if (iterable.IsError)
            return iterable;

        return PyBoolObject.FromBoolean(_set.IsSupersetOf(iterable.Value));
    }

    internal PyResult PySymmetricDifference(PyCallContext context, PyObject other)
    {
        var result = new HashSet<PyObject>(_set, PyObjectComparer.Default);
        var iterable = PyUtils.IterableToList(context, other);
        if (iterable.IsError)
            return iterable;
        result.SymmetricExceptWith(iterable.Value);
        return new PyFrozenSetObject(result);
    }

    internal PyResult PyUnion(PyCallContext context, IReadOnlyList<PyObject> others)
    {
        var result = new HashSet<PyObject>(_set, PyObjectComparer.Default);
        foreach (var other in others)
        {
            var iterable = PyUtils.IterableToList(context, other);
            if (iterable.IsError)
                return iterable;
            result.UnionWith(iterable.Value);
        }
        return new PyFrozenSetObject(result);
    }
}

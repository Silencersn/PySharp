using PySharp.Runtime;
using PySharp.Runtime.Calls;

namespace PySharp.Modules.Builtins;

partial class PySetObject
{
    [AIGenerated]
    public bool PyAdd(PyObject item)
    {
        return _set.Add(item);
    }

    [AIGenerated]
    public void PyClear()
    {
        _set.Clear();
    }

    [AIGenerated]
    public PySetObject PyCopy()
    {
        return new PySetObject(_set);
    }

    [AIGenerated]
    public PyResult PyDiscard(PyObject item)
    {
        _set.Remove(item);
        return PyNoneObject.None;
    }

    [AIGenerated]
    public PyResult PyRemove(PyObject item)
    {
        if (_set.Remove(item))
            return PyNoneObject.None;

        return PyResult.KeyError(item);
    }

    [AIGenerated]
    public PyResult<PyObject> PyPop()
    {
        if (_set.Count is 0)
            return PyResult.KeyError(PyStrObject.FromString("pop from an empty set")).Of<PyObject>();

        var item = _set.First();
        _set.Remove(item);
        return item;
    }

    [AIGenerated]
    public PyResult<PyBoolObject> PyIsDisjoint(PyCallContext context, PyObject other)
    {
        var otherIter = PySpecialMethods.Iter(context, other);
        if (otherIter.IsError)
            return otherIter.Of<PyBoolObject>();

        var iterator = otherIter.Value;
        while (true)
        {
            var item = PySpecialMethods.Next(context, iterator);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    break;
                return item.Of<PyBoolObject>();
            }

            if (_set.Contains(item.Value))
                return PyBoolObject.False;
        }

        return PyBoolObject.True;
    }

    [AIGenerated]
    public PyResult<PyBoolObject> PyIsSubset(PyCallContext context, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(_set.IsSubsetOf(otherSet._set));

        var otherResult = PyUtils.IterableToSet(context, other);
        if (otherResult.IsError)
            return otherResult.Of<PyBoolObject>();

        return PyBoolObject.FromBoolean(_set.IsSubsetOf(otherResult.Value._set));
    }

    [AIGenerated]
    public PyResult<PyBoolObject> PyIsSuperset(PyCallContext context, PyObject other)
    {
        if (other is PySetObject otherSet)
            return PyBoolObject.FromBoolean(_set.IsSupersetOf(otherSet._set));

        var otherResult = PyUtils.IterableToSet(context, other);
        if (otherResult.IsError)
            return otherResult.Of<PyBoolObject>();

        return PyBoolObject.FromBoolean(_set.IsSupersetOf(otherResult.Value._set));
    }

    [AIGenerated]
    public PyResult PyUpdate(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        foreach (var other in others)
        {
            var otherSet = PyUtils.IterableToSet(context, other);
            if (otherSet.IsError)
                return otherSet;

            _set.UnionWith(otherSet.Value._set);
        }

        return PyNoneObject.None;
    }

    [AIGenerated]
    public PyResult<PySetObject> PyUnion(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        var newSet = PyCopy();
        var result = newSet.PyUpdate(context, others);
        if (result.IsError)
            return result.Of<PySetObject>();

        return newSet;
    }

    [AIGenerated]
    public PyResult PyIntersectionUpdate(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        foreach (var other in others)
        {
            var otherSet = PyUtils.IterableToSet(context, other);
            if (otherSet.IsError)
                return otherSet;

            _set.IntersectWith(otherSet.Value._set);
        }

        return PyNoneObject.None;
    }

    [AIGenerated]
    public PyResult<PySetObject> PyIntersection(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        var newSet = PyCopy();
        var result = newSet.PyIntersectionUpdate(context, others);
        if (result.IsError)
            return result.Of<PySetObject>();

        return newSet;
    }

    [AIGenerated]
    public PyResult PyDifferenceUpdate(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        foreach (var other in others)
        {
            var otherSet = PyUtils.IterableToSet(context, other);
            if (otherSet.IsError)
                return otherSet;

            _set.ExceptWith(otherSet.Value._set);
        }

        return PyNoneObject.None;
    }

    [AIGenerated]
    public PyResult<PySetObject> PyDifference(PyCallContext context, params IReadOnlyList<PyObject> others)
    {
        var newSet = PyCopy();
        var result = newSet.PyDifferenceUpdate(context, others);
        if (result.IsError)
            return result.Of<PySetObject>();

        return newSet;
    }

    [AIGenerated]
    public PyResult PySymmetricDifferenceUpdate(PyCallContext context, PyObject other)
    {
        var otherSet = PyUtils.IterableToSet(context, other);
        if (otherSet.IsError)
            return otherSet;

        _set.SymmetricExceptWith(otherSet.Value._set);
        return PyNoneObject.None;
    }

    [AIGenerated]
    public PyResult<PySetObject> PySymmetricDifference(PyCallContext context, PyObject other)
    {
        var newSet = PyCopy();
        var result = newSet.PySymmetricDifferenceUpdate(context, other);
        if (result.IsError)
            return result.Of<PySetObject>();

        return newSet;
    }
}

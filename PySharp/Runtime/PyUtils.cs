using PySharp.Modules;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;

namespace PySharp.Runtime;

internal static class PyUtils
{
    private static PyResult<T> IterableToContainer<T>(PyCallContext context, PyObject iterable, Func<List<PyObject>, T> createContainer) where T : PyObject
    {
        var iterator = PySpecialMethods.Iter(context, iterable);
        if (iterator.IsError)
            return iterator.ExceptionResult;

        return IteratorToContainer(context, iterator.Value, createContainer);
    }

    private static PyResult<T> IteratorToContainer<T>(PyCallContext context, PyObject iterator, Func<List<PyObject>, T> createContainer) where T : PyObject
    {
        List<PyObject> list = [];

        while (true)
        {
            var item = PySpecialMethods.Next(context, iterator);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    break;

                return item.ExceptionResult;
            }

            list.Add(item.Value);
        }

        return createContainer(list);
    }

    public static PyResult<PyListObject> IterableToList(PyCallContext context, PyObject iterable)
    {
        return IterableToContainer(context, iterable, PyListObject.CreateProxy);
    }

    public static PyResult<PySetObject> IterableToSet(PyCallContext context, PyObject iterable)
    {
        return IterableToContainer(context, iterable, list => PySetObject.CreateSet(list));
    }

    public static PyResult<PyTupleObject> IterableToTuple(PyCallContext context, PyObject iterable)
    {
        return IterableToContainer(context, iterable, PyTupleObject.CreateTuple);
    }

    public static PyResult<PyListObject> IteratorToList(PyCallContext context, PyObject iterator)
    {
        return IteratorToContainer(context, iterator, PyListObject.CreateProxy);
    }

    public static PyResult<PyTupleObject> IteratorToTuple(PyCallContext context, PyObject iterator)
    {
        return IteratorToContainer(context, iterator, PyTupleObject.CreateTuple);
    }

    public static PyResult<PyDictObject> MappingToDict(PyCallContext context, PyObject mapping, PyObject keysMethod)
    {
        var keys = keysMethod.Call(context);
        if (keys.IsError)
            return keys.ExceptionResult;

        var keysList = IterableToList(context, keys.Value);
        if (keysList.IsError)
            return keysList.ExceptionResult;

        var dict = new PyDictObject();

        foreach (var key in keysList.Value)
        {
            var value = PySpecialMethods.GetItem(context, mapping, key);
            if (value.IsError)
                return value.ExceptionResult;

            var result = dict.SetItem(context, key, value.Value);
            if (result.IsError)
                return result.ExceptionResult;
        }

        return dict;
    }

    public static PyResult<PyDictObject> IterableToDict(PyCallContext context, PyObject iterable)
    {
        var pairs = IterableToList(context, iterable);
        if (pairs.IsError)
            return pairs.ExceptionResult;

        var dict = new PyDictObject();

        for (int i = 0; i < pairs.Value.Count; i++)
        {
            var pairList = IterableToList(context, pairs.Value[i]);
            if (pairList.IsError)
                return pairList.ExceptionResult;

            var count = pairList.Value.Count;
            if (count is not 2)
                return PyResult.ValueError(PySR.Runtime_Dictionary_UpdateEltLengthNotMatch, i, count);

            var key = pairList.Value[0];
            var value = pairList.Value[1];
            var result = dict.SetItem(context, key, value);
            if (result.IsError)
                return result.ExceptionResult;
        }

        return dict;
    }

    public static PyResult<PyDictObject> ToDict(PyCallContext context, PyObject iterableOrMapping)
    {
        if (iterableOrMapping is PyDictObject dict)
            return new PyDictObject(dict);

        var keysMethod = PyOperators.GetAttr(context, iterableOrMapping, "keys");
        if (keysMethod.IsSuccessful)
            return MappingToDict(context, iterableOrMapping, keysMethod.Value);
        else if (keysMethod.IsAttributeError)
            return IterableToDict(context, iterableOrMapping);
        else
            return keysMethod.ExceptionResult;
    }

    public static IEnumerable<PyResult> EnumerateIterator(PyCallContext context, PyObject iterator)
    {
        while (true)
        {
            var item = PySpecialMethods.Next(context, iterator);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                    yield break;

                yield return item;
                yield break;
            }

            yield return item.Value;
        }
    }

    public static bool TryEnumerateIterable(PyCallContext context, PyObject iterable, [NotNullWhen(true)] out IEnumerable<PyResult>? result, [NotNullWhen(false)] out PyResult? err)
    {
        var iter = PySpecialMethods.Iter(context, iterable);
        if (iter.IsError)
        {
            result = null;
            err = iter;
            return false;
        }

        result = EnumerateIterator(context, iter.Value);
        err = null;
        return true;
    }

    public static bool TryGetValue<T, TPyObject>(PyObject obj, Func<TPyObject, T> selector, T valueIfNone, out T result) where TPyObject : PyObject
    {
        if (obj is TPyObject objOfT)
        {
            result = selector(objOfT);
            return true;
        }

        if (obj is PyNoneObject)
        {
            result = valueIfNone;
            return true;
        }

        result = default!;
        return false;
    }

    public static int MapIndex(int index, int count)
    {
        if (index < 0)
            return index + count;
        return index;
    }

    public static BigInteger MapIndex(BigInteger index, BigInteger count)
    {
        if (index < 0)
            return index + count;
        return index;
    }

    public static bool IsIndexOutOfRange(int index, int count)
    {
        return index >= count || index < -count;
    }

    public static PyResult GetSequenceItem(PyCallContext context, ReadOnlySpan<PyObject> items, PyObject item, Func<List<PyObject>, PyObject> factory, string outOfRangeErrMsg)
    {
        if (item is PySliceObject slice)
        {
            var indicesResult = slice.Indices(context, items.Length, out var indices);
            if (indicesResult.IsError)
                return indicesResult;
            var (start, _, step, sliceLength) = indices;
            var resultList = new List<PyObject>(sliceLength);
            for (int i = 0, idx = start; i < sliceLength; i++, idx += step)
                resultList.Add(items[idx]);
            return factory(resultList);
        }

        var indexResult = PySpecialMethods.Index(context, item);
        if (indexResult.IsError)
            return indexResult;
        if (!indexResult.Value.IsInt32)
            return PyResult.IndexError("cannot fit 'int' into an index-sized integer");

        var index = indexResult.Value.Int32Value;
        if (IsIndexOutOfRange(index, items.Length))
            return PyResult.IndexError(outOfRangeErrMsg);

        return items[MapIndex(index, items.Length)];
    }

    public static PyResult<PyBoolObject> Contains(PyCallContext context, ReadOnlySpan<PyObject> items, PyObject item)
    {
        foreach (var element in items)
        {
            var eq = PyComparer.Eq(context, element, item);
            if (eq.IsError)
                return eq.ExceptionResult;

            if (eq.Value.BoolValue)
                return PyBoolObject.True;
        }
        return PyBoolObject.False;
    }

    public static PyResult<PyStrObject> CollectionRecursiveRepr(PyCallContext context, PyObject collection, IEnumerable<PyObject> items, string startWrapper, string endWrapper, HashSet<PyObject> ids, bool forceTrailingComma = false)
    {
        var builder = new StringBuilder().Append(startWrapper);
        var itemsCount = 0;

        if (ids.Add(collection))
        {
            bool first = true;
            foreach (var item in items)
            {
                if (!first)
                    builder.Append(", ");
                else
                    first = false;

                if (!IPyObjectRecursiveRepr.TryGetRecursiveRepr(context, item, ids, out var str, out var result))
                    return result.ExceptionResult;

                builder.Append(str.Value);
                itemsCount++;
            }
            ids.Remove(collection);
        }
        else
        {
            builder.Append("...");
        }

        // if it is circular reference, itemsCount must be zero
        if (itemsCount is 1 && forceTrailingComma)
            builder.Append(',');
        builder.Append(endWrapper);
        return PyStrObject.FromString(builder.ToString());
    }

    public static PyResult<PyStrObject> DictionaryRecursiveRepr(PyCallContext context, PyObject collection, IEnumerable<KeyValuePair<PyObject, PyObject>> pairs, string startWrapper, string endWrapper, HashSet<PyObject> ids)
    {
        var builder = new StringBuilder().Append(startWrapper);

        if (ids.Add(collection))
        {
            bool first = true;
            foreach (var pair in pairs)
            {
                if (!first)
                    builder.Append(", ");
                else
                    first = false;

                if (!IPyObjectRecursiveRepr.TryGetRecursiveRepr(context, pair.Key, ids, out var keyStr, out var keyResult))
                    return keyResult.ExceptionResult;

                if (!IPyObjectRecursiveRepr.TryGetRecursiveRepr(context, pair.Value, ids, out var valueStr, out var valueResult))
                    return valueResult.ExceptionResult;

                builder
                    .Append(keyStr.Value)
                    .Append(": ")
                    .Append(valueStr.Value);
            }
            ids.Remove(collection);
        }
        else
        {
            builder.Append("...");
        }

        builder.Append(endWrapper);
        return PyStrObject.FromString(builder.ToString());
    }

    public static bool IsDataDescriptor(PyObject obj)
    {
        var slots = obj.PyType.Slots;
        return slots.Set is not null || slots.Delete is not null;
    }
}

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
    // .NET cannot materialize containers beyond the array limit, so a hint
    // beyond it deterministically reproduces CPython's preallocation
    // MemoryError (hints in the resource-dependent band below the limit are
    // simply not used for allocation)
    internal static readonly long MaxPreallocationHint = Array.MaxLength;

    // CPython PyObject_LengthHint: len() wins when available (only its
    // TypeError falls through), then __length_hint__ is looked up on the
    // type's MRO — descriptors bind, instance attributes are ignored — and
    // called with no arguments. Only TypeError from that call falls back;
    // every other error propagates, which is what makes user errors inside
    // __length_hint__ observable (issue 178)
    internal static PyResult<PyIntObject> LengthHint(PyCallContext context, PyObject obj, long fallback)
    {
        if (obj.PyType.Slots.Len is not null)
        {
            var len = PySpecialMethods.Len(context, obj);
            if (len.IsError)
            {
                if (!PyTypeErrorObjectType.Shared.IsInstance(len.Exception))
                    return len;
            }
            else
            {
                return len.Value;
            }
        }

        if (!PyObject.TryLookupAttrInMro(obj.PyType, PySpecialNames.LengthHint, out var attr))
            return PyIntObject.FromInteger(fallback);

        var hint = attr;
        var getFunc = attr.PyType.Slots.Get;
        if (getFunc is not null)
        {
            var bound = getFunc(context, attr, obj, obj.PyType);
            if (bound.IsError)
            {
                // _PyObject_LookupSpecial swallows only AttributeError
                if (bound.IsAttributeError)
                    return PyIntObject.FromInteger(fallback);
                return bound.ExceptionResult;
            }
            hint = bound.Value;
        }

        var result = hint.Call(context);
        if (result.IsError)
        {
            // a non-callable attribute and a user TypeError both fall back
            if (PyTypeErrorObjectType.Shared.IsInstance(result.Exception))
                return PyIntObject.FromInteger(fallback);
            return result.ExceptionResult;
        }

        if (result.Value is PyNotImplementedObject)
            return PyIntObject.FromInteger(fallback);
        if (result.Value is not PyIntObject hintValue) // bool is a PyIntObject
            return PyResult.TypeError(PySR.Runtime_Sequence_LengthHintNotInteger, result.Value.PyType.FullName);
        if (hintValue.Value < 0)
            return PyResult.ValueError(PySR.Runtime_Sequence_LengthHintNegative);
        if (hintValue.Value > long.MaxValue)
            return PyResult.OverflowError(PySR.Runtime_Number_Int_TooLargeForSsize);
        return hintValue;
    }

    // CPython list_extend iter path: start iteration first (a failing
    // __iter__ wins over a failing hint), then consult the length hint on
    // the original iterable, then drain. Shared by list.__init__/extend/
    // +=/LIST_EXTEND and sorted(); the list() built-in consumes the hint,
    // tuple()/set()/dict() do not
    internal static PyResult<PyListObject> IteratorToListWithHint(PyCallContext context, PyObject iterable)
    {
        var iterator = PySpecialMethods.Iter(context, iterable);
        if (iterator.IsError)
            return iterator.ExceptionResult;

        var hint = LengthHint(context, iterable, 8);
        if (hint.IsError)
            return hint.ExceptionResult;
        if (hint.Value.Value > MaxPreallocationHint)
            return PyResult.MemoryError(null);

        return IteratorToList(context, iterator.Value);
    }

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
        // CPython set(x) is an empty set updated from the iterable: elements
        // stream in and each hash runs under the live context
        var set = new PySetObject();
        var result = set.PyUpdate(context, [iterable]);
        if (result.IsError)
            return result.ExceptionResult;

        return set;
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

    // Search-method bounds only feed a wrap-by-len or >len comparison
    // after conversion, so saturating oversized magnitudes matches
    // CPython's Py_ssize_t conversion for any int size
    public static int SaturateIndex(BigInteger index)
    {
        return index > int.MaxValue ? int.MaxValue : index < int.MinValue ? int.MinValue : (int)index;
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
            return PyResult.IndexError(PySR.Runtime_Index_CannotFitInt);

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
                return eq;

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
                    return result;

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
                    return keyResult;

                if (!IPyObjectRecursiveRepr.TryGetRecursiveRepr(context, pair.Value, ids, out var valueStr, out var valueResult))
                    return valueResult;

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

    // CPython dictobject.c/setobject.c (Py_IS_TYPE): only an exact
    // TypeError from a failed key hash re-raises with the container wording
    // (str() of the original error embedded in parentheses); subclasses and
    // any other error propagate unchanged with their identity preserved
    public static PyResult WrapHashFailure(PyCallContext context, PyObject key, PyResult error, string role)
    {
        if (!error.IsError || !ReferenceEquals(error.Exception.PyType, PyTypeErrorObjectType.Shared))
            return error;

        // CPython %S is str(exc): always the full str(), so custom
        // __str__ and multi-arg tuples render exactly as Python sees them
        var message = RenderExceptionMessage(context, error.Exception);
        return PyResult.TypeError($"cannot use '{key.PyType.Name}' as {role} ({message})");
    }

    private static string RenderExceptionMessage(PyCallContext context, PyExceptionObject? exception)
    {
        if (exception is null)
            return string.Empty;

        var inner = PySpecialMethods.Str(context, exception);
        return inner is { IsError: false } ? inner.Value.Value : string.Empty;
    }
}

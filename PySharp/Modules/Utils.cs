using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.Modules;

internal static class Utils
{
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

    public static Index ToIndex(int index)
    {
        return new Index(int.Abs(index), index < 0);
    }

    public static int MapIndex(int index, int count)
    {
        if (index < 0)
            return index + count;
        return index;
    }

    public static bool IsIndexOutOfRange(int index, int count)
    {
        return index >= count || index < -count;
    }

    public static PyResult GetListItem(IReadOnlyList<PyObject> items, int index, string? msgIfOutOfRange)
    {
        if (IsIndexOutOfRange(index, items.Count))
            return PyResult.IndexError(msgIfOutOfRange);

        return items[MapIndex(index, items.Count)];
    }

    public static bool TrySetListItem(IList<PyObject> items, int index, PyObject item)
    {
        if (IsIndexOutOfRange(index, items.Count))
            return false;

        items[MapIndex(index, items.Count)] = item;
        return true;
    }

    public static PyResult<PyStrObject> CollectionRecursiveRepr(PyCallContext context, PyObject collection, IEnumerable<PyObject> items, string startWrapper, string endWrapper, HashSet<int> ids, bool forceTrailingComma = false)
    {
        var builder = new StringBuilder().Append(startWrapper);
        var itemsCount = 0;

        if (!ids.Contains(collection.PyId))
        {
            ids.Add(collection.PyId);
            bool first = true;
            foreach (var item in items)
            {
                if (!first)
                    builder.Append(", ");
                else
                    first = false;

                if (!IPyObjectRecursiveRepr.TryGetRecursiveRepr(context, item, ids, out var str, out var result))
                    return result.Of<PyStrObject>();

                builder.Append(str.Value);
                itemsCount++;
            }
            ids.Remove(collection.PyId);
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

    public static PyResult<PyStrObject> DictionaryRecursiveRepr(PyCallContext context, PyObject collection, IEnumerable<KeyValuePair<PyObject, PyObject>> pairs, string startWrapper, string endWrapper, HashSet<int> ids)
    {
        var builder = new StringBuilder().Append(startWrapper);

        if (!ids.Contains(collection.PyId))
        {
            ids.Add(collection.PyId);
            bool first = true;
            foreach (var pair in pairs)
            {
                if (!first)
                    builder.Append(", ");
                else
                    first = false;

                if (!IPyObjectRecursiveRepr.TryGetRecursiveRepr(context, pair.Key, ids, out var keyStr, out var keyResult))
                    return keyResult.Of<PyStrObject>();

                if (!IPyObjectRecursiveRepr.TryGetRecursiveRepr(context, pair.Value, ids, out var valueStr, out var valueResult))
                    return valueResult.Of<PyStrObject>();

                builder
                    .Append(keyStr.Value)
                    .Append(": ")
                    .Append(valueStr.Value);
            }
            ids.Remove(collection.PyId);
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

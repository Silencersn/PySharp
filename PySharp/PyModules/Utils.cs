using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace PySharp.PyModules;

internal static class Utils
{
    public static IEnumerable<PyObject?> EnumerateIterator(PyObject iterator)
    {
        while (true)
        {
            var item = iterator.Next();
            if (item is null)
            {
                if (PyVirtualMachine.IsExceptionOfTypeRaised(PyStandardExceptionTypes.StopIteration))
                {
                    PyVirtualMachine.ClearException();
                    yield break;
                }

                yield return null;
                yield break;
            }

            yield return item;
        }
    }

    public static IEnumerable<PyObject?>? EnumerateIterable(PyObject iterable)
    {
        var iter = iterable.Iter();
        if (iter is null)
            return null;

        return EnumerateIterator(iter);
    }

    public static IReadOnlyList<PyObject>? EnumeratedIterator(PyObject iterator)
    {
        var list = new List<PyObject>();
        while (true)
        {
            var item = iterator.Next();
            if (item is null)
            {
                if (PyVirtualMachine.IsExceptionOfTypeRaised(PyStandardExceptionTypes.StopIteration))
                {
                    PyVirtualMachine.ClearException();
                    break;
                }

                return null;
            }

            list.Add(item);
        }
        return list;
    }

    public static IReadOnlyList<PyObject>? EnumeratedIterable(PyObject iterable)
    {
        var iter = iterable.Iter();
        if (iter is null)
            return null;

        return EnumeratedIterator(iter);
    }

    public static IEnumerable<KeyValuePair<PyObject, PyObject>>? EnumeratedDictionary(IEnumerable<PyObject> iterable)
    {
        var pairs = new List<KeyValuePair<PyObject, PyObject>>();

        int i = -1;
        foreach (var item in iterable)
        {
            var kvp = EnumeratedIterable(item);
            if (kvp is null)
                return null;

            if (kvp!.Count is not 2)
            {
                PyVirtualMachine.RaiseValueError($"dictionary update sequence element #{i} has length {kvp.Count}; 2 is required");
                return null;
            }

            pairs.Add(KeyValuePair.Create(kvp[0], kvp[1]));
        }

        return pairs;
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

    public static bool TryGetItem(IList<PyObject> items, int index, string? msgIfOutOfRange, [NotNullWhen(true)] out PyObject? result)
    {
        if (IsIndexOutOfRange(index, items.Count))
        {
            result = PyVirtualMachine.RaiseIndexError(msgIfOutOfRange);
            return false;
        }

        result = items[MapIndex(index, items.Count)];
        return true;
    }

    public static bool TrySetItem(IList<PyObject> items, int index, PyObject item, string? msgIfOutOfRange)
    {
        if (IsIndexOutOfRange(index, items.Count))
        {
            PyVirtualMachine.RaiseIndexError(msgIfOutOfRange);
            return false;
        }

        items[MapIndex(index, items.Count)] = item;
        return true;
    }

    public static PyObject? CollectionRecursiveRepr(PyObject collection, IEnumerable<PyObject> items, string startWrapper, string endWrapper, HashSet<int> ids)
    {
        var builder = new StringBuilder().Append(startWrapper);

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

                if (!IPyObjectRecursiveRepr.TryGetRecursiveRepr(item, ids, out var str))
                    return null;

                builder.Append(str.Value);
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

    public static PyObject? DictionaryRecursiveRepr(PyObject collection, IEnumerable<KeyValuePair<PyObject, PyObject>> pairs, string startWrapper, string endWrapper, HashSet<int> ids)
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

                if (!IPyObjectRecursiveRepr.TryGetRecursiveRepr(pair.Key, ids, out var keyStr))
                    return null;

                if (!IPyObjectRecursiveRepr.TryGetRecursiveRepr(pair.Value, ids, out var valueStr))
                    return null;

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

    public static bool IsPyObjectMethodOverridden(Type type, string name)
    {
        return IsPyObjectMethodOverride(type, name, out _);
    }
    public static bool IsPyObjectMethodOverride(Type type, string name, out MethodInfo method)
    {
        return (method = type.GetMethod(name)!).DeclaringType != typeof(PyObject);
    }

    private static readonly ConcurrentDictionary<Type, (bool IsDescriptor, bool HasGet, bool HasSet, bool HasDelete)> _isDescriptorCache = [];
    public static bool IsDescriptor(PyObject pyObj)
    {
        return IsDescriptor(pyObj, out _, out _, out _);
    }
    public static bool IsDescriptor(PyObject pyObj, out bool hasGet, out bool hasSet, out bool hasDelete)
    {
        // TODO: ClassDefNode.CustomObject is dynamic
        (var isDescriptor, hasGet, hasSet, hasDelete) = _isDescriptorCache.GetOrAdd(pyObj.GetType(), static type =>
        {
            var hasGet = IsPyObjectMethodOverridden(type, nameof(PyObject.Get));
            var hasSet = IsPyObjectMethodOverridden(type, nameof(PyObject.Set));
            var hasDelete = IsPyObjectMethodOverridden(type, nameof(PyObject.Delete));
            return (hasGet || hasSet || hasDelete, hasGet, hasSet, hasDelete);
        });
        return isDescriptor;
    }

    public static bool TryCastStrAsArg(PyObject pyObj, [NotNullWhen(true)] out string? str, string? argName = null)
    {
        if (pyObj is not PyStrObject strObj)
        {
            PyVirtualMachine.RaiseTypeError($"{argName ?? "arg"} must be string, not {pyObj.PyType.Name}");
            str = null;
            return false;
        }

        str = strObj.Value;
        return true;
    }
}

using PySharp.Modules.Builtins;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace PySharp.Runtime;

internal sealed class PyAttachedPropertiesManager
{
    private const int PyIdEmpty = 0;

    public static PyAttachedPropertiesManager Shared { get; } = new();

    private long _pyIdCounter = PyIdEmpty;
    private readonly ConditionalWeakTable<PyObject, PyAttachedProperties> _properties = [];

    private PyAttachedProperties GetProperties(PyObject obj)
    {
        return _properties.GetOrAdd(obj, static _ => new PyAttachedProperties());
    }

    internal long GetId(PyObject obj)
    {
        var properties = GetProperties(obj);
        var id = Interlocked.Read(ref properties.Id);
        if (id is not PyIdEmpty)
            return id;

        lock (properties)
        {
            if (properties.Id is not PyIdEmpty)
                return properties.Id;

            return properties.Id = Interlocked.Increment(ref _pyIdCounter);
        }
    }

    internal IDictionary<string, PyObject> GetDict(PyObject obj)
    {
        var properties = GetProperties(obj);
        if (properties.Dict is not null)
            return properties.Dict;

        lock (properties)
        {
            if (properties.Dict is not null)
                return properties.Dict;

            return properties.Dict = new PyDictObject();
        }
    }

    internal void SetDict(PyObject obj, IDictionary<string, PyObject> dict)
    {
        var properties = GetProperties(obj);
        lock (properties)
            properties.Dict = dict;
    }

    private sealed class PyAttachedProperties
    {
        public long Id;
        public IDictionary<string, PyObject>? Dict;
    }
}

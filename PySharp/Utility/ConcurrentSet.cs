using System.Collections;
using System.Collections.Concurrent;

namespace PySharp.Utility;

internal sealed class ConcurrentSet<T> : IEnumerable<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dict = [];

    public bool Add(T item)
    {
        return _dict.TryAdd(item, default);
    }

    public bool Remove(T item)
    {
        return _dict.TryRemove(item, out _);
    }

    public bool Contains(T item)
    {
        return _dict.ContainsKey(item);
    }

    public void Clear()
    {
        _dict.Clear();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _dict.Keys.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

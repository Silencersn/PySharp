using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PySharp.Utility;

/// <summary>
/// A pool for <see cref="ImmutableArray{T}.Builder"/> that reuses builder instances to reduce allocations.
/// 
/// NOT thread-safe — the caller must ensure single-threaded access.
/// Not returning a rented builder is acceptable (the builder will simply be GC'd).
/// However, builders created from elsewhere (not obtained via <see cref="Rent{T}"/>) must NEVER be
/// passed to <see cref="Return{T}"/> or <see cref="ToImmutableThenReturn{T}"/>.
/// </summary>
internal sealed class UnsafeImmutableArrayBuilderPool : IDisposable
{
    private ObjectPool<ImmutableArray<object>.Builder> _pool = new(capacity: 16, ImmutableArray.CreateBuilder<object>);

    internal ImmutableArray<T>.Builder Rent<T>()
    {
        if (typeof(T).IsValueType)
            return ImmutableArray.CreateBuilder<T>();

        var builder = _pool.Rent();
        return Unsafe.As<ImmutableArray<object>.Builder, ImmutableArray<T>.Builder>(ref builder);
    }

    internal ImmutableArray<T> ToImmutableThenReturn<T>(ImmutableArray<T>.Builder builderOfT)
    {
        if (typeof(T).IsValueType)
            return builderOfT.DrainToImmutable();

        Debug.Assert(builderOfT is ImmutableArray<object>.Builder);

        var result = builderOfT.ToImmutable();
        var builder = Unsafe.As<ImmutableArray<T>.Builder, ImmutableArray<object>.Builder>(ref builderOfT);
        builder.Clear();
        _pool.Return(builder);
        return result;
    }

    internal void Return<T>(ImmutableArray<T>.Builder builderOfT)
    {
        if (typeof(T).IsValueType)
            return;

        Debug.Assert(builderOfT is ImmutableArray<object>.Builder);

        var builder = Unsafe.As<ImmutableArray<T>.Builder, ImmutableArray<object>.Builder>(ref builderOfT);
        builder.Clear();
        _pool.Return(builder);
    }

    public void Dispose()
    {
        if (_pool is null)
            return;

        _pool.Dispose();
        _pool = null!;
    }
}

/// <summary>
/// A simple, non-thread-safe object pool backed by <see cref="ArrayPool{T}"/>.
/// </summary>
public sealed class ObjectPool<T> : IDisposable where T : notnull
{
    private T[] _array;
    private readonly Func<T> _factory;
    private readonly int _capacity;
    private int _size;

    public ObjectPool(int capacity, Func<T> factory)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentNullException.ThrowIfNull(factory);

        _array = ArrayPool<T>.Shared.Rent(capacity);
        _capacity = capacity;
        _factory = factory;
        _size = 0;
    }

    public T Rent()
    {
        if (_size is 0)
            return _factory();

        var obj = _array[--_size];
        _array[_size] = default!;
        return obj;
    }

    public void Return(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        if (_size == _capacity)
            return;

        _array[_size++] = obj;
    }

    public void Dispose()
    {
        if (_array is null)
            return;
        ArrayPool<T>.Shared.Return(_array, clearArray: true);
        _array = null!;
    }
}
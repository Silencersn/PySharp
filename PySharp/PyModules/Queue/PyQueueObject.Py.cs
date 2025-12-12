using PySharp.PyModules.Builtins;

namespace PySharp.PyModules.Queue;

partial class PyQueueObject
{
    public int PyQSize()
    {
        return _queue.Count;
    }

    public bool PyEmpty()
    {
        return _queue.IsEmpty;
    }

    public bool PyFull()
    {
        return _collection.Count == _collection.BoundedCapacity;
    }

    public PyExceptionType? PyPut(PyObject item, bool block = true, double? timeout = null)
    {
        if (_collection.IsAddingCompleted)
            return PyShutDownObjectType.Shared;

        if (block)
        {
            if (timeout is null)
            {
                _collection.Add(item);
                Interlocked.Increment(ref _unfinished_tasks);
                return null;
            }
            else
            {
                ArgumentOutOfRangeException.ThrowIfNegative(timeout.Value);

                if (!_collection.TryAdd(item, (int)TimeSpan.FromSeconds(timeout.Value).TotalMilliseconds))
                    return PyFullObjectType.Shared;

                Interlocked.Increment(ref _unfinished_tasks);
                return null;
            }
        }
        else
        {
            if (!_collection.TryAdd(item))
                return PyFullObjectType.Shared;

            Interlocked.Increment(ref _unfinished_tasks);
            return null;
        }
    }

    public (PyObject? Result, PyExceptionType? Exception) PyGet(bool block = true, double? timeout = null)
    {
        if (_collection.IsCompleted)
            return (null, PyShutDownObjectType.Shared);

        if (block)
        {
            if (timeout is null)
            {
                return (_collection.Take(), null);
            }
            else
            {
                ArgumentOutOfRangeException.ThrowIfNegative(timeout.Value);

                var successful = _collection.TryTake(out var item, (int)TimeSpan.FromSeconds(timeout.Value).TotalMilliseconds);
                return successful ? (item, null) : (null, PyEmptyObjectType.Shared);
            }
        }
        else
        {
            var successful = _collection.TryTake(out var item);
            return successful ? (item, null) : (null, PyEmptyObjectType.Shared);
        }
    }

    public bool PyTryTaskDone()
    {
        if (Interlocked.Decrement(ref _unfinished_tasks) < 0)
        {
            Interlocked.Increment(ref _unfinished_tasks);
            return false;
        }

        // allows other threads to call PyPut to Increment _unfinished_tasks here

        if (_source is not null && _unfinished_tasks is 0)
        {
            // no lock, because the queue is joining
            // no others could change the _source
            if (_source.TrySetResult())
                _source = null;
        }

        return true;
    }

    private readonly Lock _sourceLock = new();
    public void PyJoin()
    {
        TaskCompletionSource source;

        // prevent TaskDone between creating and waiting
        lock (_sourceLock)
        {
            // if the queue is joining on other thread, do not create new one
            _source ??= new TaskCompletionSource();
            source = _source;
        }

        source.Task.Wait();
    }
}

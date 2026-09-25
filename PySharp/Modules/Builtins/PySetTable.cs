using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;

namespace PySharp.Modules.Builtins;

// Hash table backing set/frozenset storage. The skeleton follows .NET
// HashSet<T> (bucket chains into a slot array with a free list, slot-order
// enumeration) with two CPython setobject.c adaptations: each slot carries
// the element's full 64-bit hash (setentry.hash), computed once by the
// caller at operation entry, and probing compares that hash before invoking
// Python __eq__ with the stored element as the left operand, matching
// set_lookkey's RichCompareBool(startkey, key, Py_EQ).
internal sealed class PySetTable
{
    internal struct Slot
    {
        public int Next;
        public long Hash;
        public PyObject? Key;
    }

    private int[] _buckets;
    private Slot[] _slots;
    private int _count;
    private int _freeList;
    private int _freeCount;

    public PySetTable()
    {
        _buckets = [];
        _slots = [];
        _freeList = -1;
    }

    // live element count (CPython's so->used)
    public int Count => _count - _freeCount;

    internal int SlotCount => _count;

    internal PyObject? GetKeyAt(int slotIndex) => _slots[slotIndex].Key;

    private ref int GetBucket(long hash)
    {
        return ref _buckets[(ulong)hash % (uint)_buckets.Length];
    }

    internal IEnumerable<PyObject> Keys
    {
        get
        {
            for (var i = 0; i < _count; i++)
            {
                if (_slots[i].Key is { } key)
                    yield return key;
            }
        }
    }

    // CPython set_repr copies the keys into a list before repr'ing them
    // (set_repr_lock_held, gh-129967): element __repr__ may mutate the set,
    // and the snapshot keeps those mutations out of the rendered text.
    internal PyObject[] SnapshotKeys() => Keys.ToArray();

    internal IEnumerable<(PyObject Key, long Hash)> LiveEntries
    {
        get
        {
            for (var i = 0; i < _count; i++)
            {
                if (_slots[i].Key is { } key)
                    yield return (key, _slots[i].Hash);
            }
        }
    }

    // CPython set_lookkey: full-hash fast reject, identity shortcut, then
    // Python equality. Returns null on success (index -1 when absent) or
    // the comparison error.
    private PyExceptionObject? Probe(PyCallContext context, PyObject key, long hash, out int index)
    {
        index = -1;
        if (_buckets.Length is 0)
            return null;

        var current = _buckets[(ulong)hash % (uint)_buckets.Length] - 1;
        while (current >= 0)
        {
            ref var slot = ref _slots[current];
            if (slot.Hash == hash)
            {
                var stored = slot.Key;
                if (ReferenceEquals(stored, key))
                {
                    index = current;
                    return null;
                }

                var slots = _slots;
                var countBefore = Count;
                var eq = PyComparer.Eq(context, stored, key);
                if (eq.IsError)
                    return eq.Exception;

                // a comparison may mutate the table (resize, removal of the
                // stored element, or a same-bucket insert the walk has
                // already passed) and invalidate it — restart from the
                // bucket head, like set_lookkey re-walking from so->table;
                // the chain end below still terminates. An adversarial
                // __eq__ that mutates the set on every call restarts
                // unboundedly — CPython's restart recursion is equally
                // unbounded for the same input.
                if (Count != countBefore
                    || !ReferenceEquals(slots, _slots)
                    || !ReferenceEquals(slots[current].Key, stored))
                {
                    current = _buckets.Length is 0
                        ? -1
                        : _buckets[(ulong)hash % (uint)_buckets.Length] - 1;
                    continue;
                }

                if (eq.Value.BoolValue)
                {
                    index = current;
                    return null;
                }
            }

            current = slot.Next;
        }

        return null;
    }

    public PyResult<PyBoolObject> Contains(PyCallContext context, PyObject key, long hash)
    {
        var error = Probe(context, key, hash, out var index);
        if (error is not null)
            return PyResult.FromException(error).Of<PyBoolObject>();

        return PyBoolObject.FromBoolean(index >= 0);
    }

    // true when the element was newly added
    public PyResult<PyBoolObject> Add(PyCallContext context, PyObject key, long hash)
    {
        var error = Probe(context, key, hash, out var index);
        if (error is not null)
            return PyResult.FromException(error).Of<PyBoolObject>();
        if (index >= 0)
            return PyBoolObject.False;

        Insert(key, hash);
        return PyBoolObject.True;
    }

    // true when the element was present and removed
    public PyResult<PyBoolObject> Remove(PyCallContext context, PyObject key, long hash)
    {
        var error = Probe(context, key, hash, out var index);
        if (error is not null)
            return PyResult.FromException(error).Of<PyBoolObject>();
        if (index < 0)
            return PyBoolObject.False;

        Unlink(index, hash);
        return PyBoolObject.True;
    }

    // CPython set_pop: remove and return an arbitrary element (null when empty)
    internal PyObject? PopFirst()
    {
        for (var i = 0; i < _count; i++)
        {
            if (_slots[i].Key is null)
                continue;

            var key = _slots[i].Key;
            Unlink(i, _slots[i].Hash);
            return key;
        }

        return null;
    }

    public void Clear()
    {
        _buckets = [];
        _slots = [];
        _count = 0;
        _freeList = -1;
        _freeCount = 0;
    }

    // adopt another table's contents wholesale (the source must not be
    // referenced afterwards — its arrays are taken over, not copied)
    internal void ReplaceWith(PySetTable other)
    {
        _buckets = other._buckets;
        _slots = other._slots;
        _count = other._count;
        _freeList = other._freeList;
        _freeCount = other._freeCount;
    }

    // CPython set_merge fast path: direct entry transfer with the stored
    // hashes — no probing, so no user callbacks can fire
    public PySetTable Clone()
    {
        var copy = new PySetTable();
        var live = Count;
        if (live is 0)
            return copy;

        var size = live * 2;
        copy._buckets = new int[size];
        copy._slots = new Slot[size];

        var index = 0;
        for (var i = 0; i < _count; i++)
        {
            if (_slots[i].Key is null)
                continue;

            copy._slots[index] = _slots[i];
            PushIntoBucket(copy._buckets, copy._slots, index);
            index++;
        }

        copy._count = live;
        return copy;
    }

    // CPython set_difference_update_internal: dummies beyond a quarter of
    // the table are compacted away
    internal void CompactIfSparse()
    {
        if (_freeCount > 0 && _buckets.Length is not 0 && _freeCount > _buckets.Length / 4)
            Resize(Count * 2);
    }

    private void Insert(PyObject key, long hash)
    {
        int index;
        if (_freeCount > 0)
        {
            index = _freeList;
            _freeList = _slots[index].Next;
            _freeCount--;
        }
        else
        {
            if (_count == _slots.Length)
                Resize(Count * 2 + 4);

            index = _count++;
        }

        _slots[index].Key = key;
        _slots[index].Hash = hash;
        PushIntoBucket(_buckets, _slots, index);
    }

    private void Unlink(int index, long hash)
    {
        ref var slot = ref _slots[index];
        slot.Key = null;

        ref var bucket = ref GetBucket(hash);
        if (bucket == index + 1)
        {
            bucket = slot.Next + 1;
        }
        else
        {
            var walk = bucket - 1;
            while (_slots[walk].Next != index)
                walk = _slots[walk].Next;

            _slots[walk].Next = slot.Next;
        }

        slot.Next = _freeList;
        _freeList = index;
        _freeCount++;
    }

    // compaction resize: live slots are packed to the front, free-list
    // holes disappear, and re-bucketing reuses the stored hashes only
    private void Resize(int newSize)
    {
        var newBuckets = new int[newSize];
        var newSlots = new Slot[newSize];

        var live = 0;
        for (var i = 0; i < _count; i++)
        {
            if (_slots[i].Key is null)
                continue;

            newSlots[live] = _slots[i];
            PushIntoBucket(newBuckets, newSlots, live);
            live++;
        }

        _buckets = newBuckets;
        _slots = newSlots;
        _count = live;
        _freeList = -1;
        _freeCount = 0;
    }

    private static void PushIntoBucket(int[] buckets, Slot[] slots, int index)
    {
        ref var slot = ref slots[index];
        ref var bucket = ref buckets[(ulong)slot.Hash % (uint)buckets.Length];
        slot.Next = bucket - 1;
        bucket = index + 1;
    }
}

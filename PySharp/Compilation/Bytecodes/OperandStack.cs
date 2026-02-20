using PySharp.Modules.Builtins;

namespace PySharp.Compilation.Bytecodes;

internal sealed class OperandStack
{
    private int _size = 0;
    private PyObject[] _array = [];

    public int Count => _size;

    public PyObject this[int index]
    {
        get => _array[_size + index];
        set => _array[_size + index] = value;
    }

    public void Push(PyObject value)
    {
        if (_size == _array.Length)
            Grow(_size + 1);

        _array[_size++] = value;
    }
    public void PushRange(params ReadOnlySpan<PyObject> values)
    {
        if (_size + values.Length > _array.Length)
            Grow(_size + values.Length);

        values.CopyTo(_array.AsSpan()[_size..]);
        _size += values.Length;
    }
    public void PushReversedRange(params ReadOnlySpan<PyObject> values)
    {
        PushRange(values);
        _array.AsSpan().Slice(_size - values.Length, values.Length).Reverse();
    }
    public PyObject Peek()
    {
        return _array[_size - 1];
    }
    public PyObject Pop()
    {
        var value = _array[--_size];
        _array[_size] = null!;
        return value;
    }
    public void PopReversedRange(Span<PyObject> values)
    {
        var span = _array.AsSpan().Slice(_size - values.Length, values.Length);
        span.CopyTo(values);
        span.Clear();
        _size -= values.Length;
    }
    public void Clear()
    {
        _array.AsSpan()[.._size].Clear();
        _size = 0;
    }
    private void Grow(int capacity)
    {
        const int DefaultCapacity = 8;
        int newCapacity = _array.Length == 0 ? DefaultCapacity : 2 * _array.Length;
        if (newCapacity < capacity)
            newCapacity = capacity;
        Array.Resize(ref _array, newCapacity);
    }
}
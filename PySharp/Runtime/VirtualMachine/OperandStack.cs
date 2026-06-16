using PySharp.Modules.Builtins;
using System.Buffers;
using System.Diagnostics;

namespace PySharp.Runtime.VirtualMachine;

internal sealed class OperandStack : IDisposable
{
    private int _size = 0;
    private PyObject[] _array;

    public int Count
    {
        get => _size;
        set => _size = value;
    }

    internal OperandStack(int stackSize)
    {
        _array = ArrayPool<PyObject>.Shared.Rent(stackSize);
    }

    public void Push(PyObject value)
    {
        _array[_size++] = value;
    }

    public void Dispose()
    {
        ArrayPool<PyObject>.Shared.Return(_array);
        _array = null!;
    }

    public ValueOperandStack AsValueOperandStack()
    {
        return new ValueOperandStack(_array, _size);
    }
}

internal ref struct ValueOperandStack
{
    private int _size;
    private readonly Span<PyObject> _span;

    public readonly int Count => _size;

    public readonly ref PyObject this[int index]
    {
        get
        {
            Debug.Assert(index < 0);
            return ref _span[_size + index];
        }
    }

    internal ValueOperandStack(Span<PyObject> span, int size = 0)
    {
        _span = span;
        _size = size;
    }

    internal void SetSize(int size)
    {
        _size = size;
    }

    public void Push(PyObject value)
    {
        _span[_size++] = value;
    }
    public void PushRange(params ReadOnlySpan<PyObject> values)
    {
        values.CopyTo(_span[_size..]);
        _size += values.Length;
    }
    public void PushReversedRange(params ReadOnlySpan<PyObject> values)
    {
        PushRange(values);
        _span.Slice(_size - values.Length, values.Length).Reverse();
    }
    public readonly PyObject Peek()
    {
        return _span[_size - 1];
    }
    public PyObject Pop()
    {
        var value = _span[--_size];
        _span[_size] = null!;
        return value;
    }
    public void PopReversedRange(Span<PyObject> values)
    {
        var span = _span.Slice(_size - values.Length, values.Length);
        span.CopyTo(values);
        span.Clear();
        _size -= values.Length;
    }
    public void Clear()
    {
        _span[.._size].Clear();
        _size = 0;
    }
}
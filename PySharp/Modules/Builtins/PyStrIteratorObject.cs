using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public class PyStrIteratorObject : PyObject
{
    // iterate in code units: System.Rune cannot represent an unpaired
    // surrogate, which a CPython str iterator yields as itself
    internal readonly string _value;
    internal int _index;

    public override PyTypeObject DefaultPyType { get; }

    internal PyStrIteratorObject(string str, bool asciiOnly)
    {
        // CPython picks the iterator type from the string's storage form:
        // a UCS-1 ascii string reports str_ascii_iterator, everything
        // else (any non-ascii kind) reports str_iterator
        DefaultPyType = asciiOnly ? PyStrAsciiIteratorObjectType.Shared : PyStrIteratorObjectType.Shared;
        _value = str;
    }

    internal PyResult Next()
    {
        if (_index >= _value.Length)
            return PyResult.StopIteration();

        var width = PyStrObject.CharWidthAt(_value, _index);
        var codePoint = width is 2
            ? char.ConvertToUtf32(_value[_index], _value[_index + 1])
            : _value[_index];
        _index += width;
        return PyStrObject.FromCodePoint(codePoint);
    }
}

[PyType("str_iterator")]
public sealed partial class PyStrIteratorObjectType : PyTypeObject<PyStrIteratorObject>
{

    protected override PyResult Iter(PyCallContext context, PyStrIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyStrIteratorObject self)
    {
        return self.Next();
    }
}

[PyType("str_ascii_iterator")]
public sealed partial class PyStrAsciiIteratorObjectType : PyTypeObject<PyStrIteratorObject>
{

    protected override PyResult Iter(PyCallContext context, PyStrIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyStrIteratorObject self)
    {
        return self.Next();
    }
}

using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public class PyStrIteratorObject : PyObject
{
    // iterate in code units: System.Rune cannot represent an unpaired
    // surrogate, which a CPython str iterator yields as itself
    internal readonly string _value;
    internal int _index;

    public override PyTypeObject DefaultPyType => PyStrIteratorObjectType.Shared;

    internal PyStrIteratorObject(string str)
    {
        _value = str;
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
        if (self._index >= self._value.Length)
            return PyResult.StopIteration();

        var width = PyStrObject.CharWidthAt(self._value, self._index);
        var codePoint = width is 2
            ? char.ConvertToUtf32(self._value[self._index], self._value[self._index + 1])
            : self._value[self._index];
        self._index += width;
        return PyStrObject.FromCodePoint(codePoint);
    }
}

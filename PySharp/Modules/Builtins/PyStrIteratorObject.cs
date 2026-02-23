using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Text;

namespace PySharp.Modules.Builtins;

public class PyStrIteratorObject : PyObject
{
    internal StringRuneEnumerator _enumerator;
    public override PyTypeObject DefaultPyType => PyStrIteratorObjectType.Shared;

    internal PyStrIteratorObject(string str)
    {
        _enumerator = str.EnumerateRunes();
    }
}

[PyType("str_iterator")]
public sealed partial class PyStrIteratorObjectType : PyTypeObject<PyStrIteratorObjectType, PyStrIteratorObject>
{

    protected override PyResult Iter(PyCallContext context, PyStrIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyStrIteratorObject self)
    {
        if (!self._enumerator.MoveNext())
            return PyResult.StopIteration();
        return PyStrObject.FromRune(self._enumerator.Current);
    }
}
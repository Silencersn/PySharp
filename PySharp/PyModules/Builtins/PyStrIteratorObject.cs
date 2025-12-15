using PySharp.PyRuntime;
using System.Text;

namespace PySharp.PyModules.Builtins;

public class PyStrIteratorObject : PyObject
{
    private StringRuneEnumerator _enumerator;

    public override PyTypeObject PyType => PyStrIteratorObjectType.Shared;

    internal PyStrIteratorObject(string str)
    {
        _enumerator = str.EnumerateRunes();
    }

    public override PyObject? Iter()
    {
        return this;
    }

    public override PyObject? Next()
    {
        if (!_enumerator.MoveNext())
            return PyVirtualMachine.RaiseStopIteration();

        return PyStrObject.FromRune(_enumerator.Current);
    }
}

public sealed class PyStrIteratorObjectType : PyPrimitiveTypeObject<PyStrIteratorObjectType, PyStrIteratorObject>
{
    public override string Name => "str_iterator";
}
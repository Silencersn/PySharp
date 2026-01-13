using PySharp.PyRuntime.Calls;
using System.Text;

namespace PySharp.PyModules.Builtins;

public class PyStrIteratorObject : PyObject
{
    internal StringRuneEnumerator _enumerator;
    public override PyTypeObject DefaultPyType => PyStrIteratorObjectType.Shared;

    internal PyStrIteratorObject(string str)
    {
        _enumerator = str.EnumerateRunes();
    }
}

public sealed class PyStrIteratorObjectType : PyTypeObject<PyStrIteratorObjectType, PyStrIteratorObject>
{
    public override string Module => "builtins";
    public override string Name => "str_iterator";

    protected override PyResult Iter(PyCallContext context, PyStrIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyStrIteratorObject self)
    {
        if (!self._enumerator.MoveNext())
            return PyResult.RaiseStopIteration();
        return PyStrObject.FromRune(self._enumerator.Current);
    }
}
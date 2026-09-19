using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public class PySetIteratorObject : PyObject
{
    // CPython setiterobject: the source table is released when the iterator
    // is exhausted (si_set = NULL), after which size changes go unnoticed;
    // the used-count snapshot (-1 once the error turned sticky) drives the
    // "Set changed size during iteration" check on each next()
    internal PySetTable? _table;
    internal long _used;
    internal int _pos;
    internal long _len;

    public override PyTypeObject DefaultPyType => PySetIteratorObjectType.Shared;

    public PySetIteratorObject(PySetObject pySetObject)
    {
        ArgumentNullException.ThrowIfNull(pySetObject);
        _table = pySetObject._table;
        _used = _table.Count;
        _len = _used;
    }

    public PySetIteratorObject(PyFrozenSetObject pyFrozenSetObject)
    {
        ArgumentNullException.ThrowIfNull(pyFrozenSetObject);
        _table = pyFrozenSetObject._table;
        _used = _table.Count;
        _len = _used;
    }
}

[PyType("set_iterator")]
public sealed partial class PySetIteratorObjectType : PyTypeObject<PySetIteratorObject>
{
    protected override PyResult Iter(PyCallContext context, PySetIteratorObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PySetIteratorObject self)
    {
        var table = self._table;
        if (table is null)
            return PyResult.StopIteration();

        if (self._used is -1)
            return PyResult.RuntimeError(PySR.Runtime_Set_IterationChangedSize);

        if (self._used != table.Count)
        {
            // make this state sticky, like CPython's si_used = -1
            self._used = -1;
            return PyResult.RuntimeError(PySR.Runtime_Set_IterationChangedSize);
        }

        while (self._pos < table.SlotCount)
        {
            var key = table.GetKeyAt(self._pos++);
            if (key is not null)
            {
                self._len--;
                return key;
            }
        }

        self._table = null;
        return PyResult.StopIteration();
    }

    // CPython setiter_len: the remaining count is only reported while the
    // iterator has not observed a size change
    [PyMethod(PySpecialNames.LengthHint)]
    [PyFunctionParameters()]
    private static PyResult LengthHint(PyCallContext context, PySetIteratorObject self, PyArguments arguments)
    {
        if (self._table is { } table && self._used is not -1 && self._used == table.Count)
            return PyIntObject.FromInteger(self._len);

        return PyIntObject.Zero;
    }
}

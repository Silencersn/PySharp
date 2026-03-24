using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject
{
    [PySlot]
    protected virtual PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.TypeError(PySR.Runtime_Type_CannotCreateInstance, cls.FullName);
    }
}

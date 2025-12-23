using PySharp.PyRuntime;

namespace PySharp.PyModules.Builtins;

partial class PyObject
{
    protected internal virtual PyObject? ReprImpl()
    {
        return PyStrObject.FromString($"<{PyType.Name} object at 0x{PyId:X16}>");
    }

    protected internal virtual PyObject? StrImpl()
    {
        return Repr();
    }

    protected internal virtual PyObject? BoolImpl()
    {
        return PyBoolObject.True;
    }
}

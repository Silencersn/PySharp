using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

partial class PyTypeObject
{
    internal PyResult DefaultRepr(PyCallContext context, PyObject self)
    {
        return PyStrObject.FromString($"<{FullName} object at 0x{self.PyId:X16}>");
    }
    internal PyResult DefaultStr(PyCallContext context, PyObject self)
    {
        return self.Repr(context);
    }
    internal PyResult DefaultBool(PyCallContext context, PyObject self)
    {
        return PyBoolObject.True;
    }
    internal PyResult DefaultHash(PyCallContext context, PyObject self)
    {
        return PyIntObject.FromInteger(self.GetHashCode());
    }
}

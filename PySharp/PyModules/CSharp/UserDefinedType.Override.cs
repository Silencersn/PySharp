using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.CSharp;

partial class UserDefinedType<TObject> : PyTypeObject<TObject> where TObject : PyObject
{
    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (TryLookupAttrInMro(this, PySpecialNames.New, out var method))
            return method.Call(context, [cls, .. args], kwargs);
        return Bases[0].New(context, cls, args, kwargs);
    }
}
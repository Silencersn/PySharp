using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace PySharp.PyModules.Builtins;

public abstract partial class PyTypeObject<TObject> : PyTypeObject where TObject : PyObject
{
    public sealed override Type LayoutType => typeof(TObject);
    internal sealed override bool IsPyTypeObjectOfT => true;

    public PyTypeObject()
    {
        AppendMethodDescriptors();
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.RaiseTypeError($"cannot create '{Name}' instances");
    }
}

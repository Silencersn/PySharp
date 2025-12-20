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
    public override PyTypeObject DefaultPyType => PyTypeObjectType2.Shared;

    public PyTypeObject()
    {
        AppendMethodDescriptors();
    }

    public PyTypeObject(string name, IReadOnlyList<PyTypeObject> bases) : base(name, bases)
    {
        AppendMethodDescriptors();
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.RaiseTypeError($"cannot create '{Name}' instances");
    }
}

public sealed class PyTypeObjectType2 : PyTypeObject<PyTypeObject>
{
    public static PyTypeObjectType2 Shared { get; } = new();
    public override string Name => "type";

    protected internal override PyResult Call(PyCallContext context, PyTypeObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var result = self.New(context, self, args, kwargs);
        if (result.IsError)
            return result;

        var pyObject = result.Value;
        if (self.IsInstance(pyObject))
        {
            var initResult = self.Init(context, pyObject, args, kwargs);
            if (initResult.IsError)
                return initResult;
        }

        return pyObject;
    }
}
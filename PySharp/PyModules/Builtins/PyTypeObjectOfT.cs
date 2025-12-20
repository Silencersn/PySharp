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
        AppendOverridenMethodDescriptors();
    }

    public PyTypeObject(string name, IReadOnlyList<PyTypeObject> bases, bool appendOverridenMethods) : base(name, bases)
    {
        if (appendOverridenMethods)
            AppendOverridenMethodDescriptors();
    }

    protected internal override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyResult.RaiseTypeError($"cannot create '{Name}' instances");
    }
}

public sealed class PyTypeObjectType2 : PyTypeObject<PyTypeObjectType2, PyTypeObject>
{
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

public abstract class PyTypeObject<TSelf, TObject> : PyTypeObject<TObject>
    where TSelf : PyTypeObject<TSelf, TObject>, new()
    where TObject : PyObject
{
    public static TSelf Shared { get; } = new();
}
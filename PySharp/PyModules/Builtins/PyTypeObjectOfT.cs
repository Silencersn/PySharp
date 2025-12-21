using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace PySharp.PyModules.Builtins;

public abstract partial class PyTypeObject<TObject> : PyTypeObject where TObject : PyObject
{
    public sealed override Type LayoutType => typeof(TObject);
    internal sealed override bool IsPyTypeObjectOfT => true;
    public override PyTypeObject DefaultPyType => PyTypeObjectType2.Shared;

    public PyTypeObject()
    {
        AppendOverridenSpecialMethodDescriptors2();

        var newMethod = GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(method => method.Name == "New" && method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject));
        if (newMethod.DeclaringType != typeof(PyTypeObject<TObject>))
        {
            Console.WriteLine(GetType());
            var method = new PyBuiltinFunctionOrMethodObject2(PySpecialNames.New, this, null! /* TODO */, [PyFunctionArgsDef("cls", "*args", "**kwargs")] (context, arguments) =>
            {
                if (arguments[0] is not PyTypeObject cls)
                    return PyResult.RaiseTypeError(null);

                if (!cls.IsSubclassOf(this))
                    return PyResult.RaiseTypeError($"{Name}.__new__({cls.Name}): {cls.Name} is not a subtype of {Name}");

                if (cls.LayoutType.IsSubclassOf(LayoutType))
                    return PyResult.RaiseTypeError($"{Name}.__new__({cls.Name}) is not safe, use {cls.Name}.__new__()");
                Debug.Assert(cls.LayoutType == LayoutType || LayoutType.IsSubclassOf(cls.LayoutType));

                return New(context, cls, arguments.ExtraArgs, arguments.ExtraKwargs);
            });
            PyAttributes.Add(PySpecialNames.New, method);
        }
    }

    public PyTypeObject(string name, IReadOnlyList<PyTypeObject> bases, bool appendOverridenMethods) : base(name, bases)
    {
        if (appendOverridenMethods)
            AppendOverridenSpecialMethodDescriptors2();
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
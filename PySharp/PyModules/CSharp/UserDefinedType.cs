using PySharp.PyModules.Builtins;
using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace PySharp.PyModules.CSharp;

public static class UserDefinedType
{
    public static PyTypeObject Create(Type layout, string name, string qualName, IReadOnlyList<PyTypeObject> bases)
    {
        var type = typeof(UserDefinedType<>).MakeGenericType(layout);
        var result = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [name, qualName, bases], CultureInfo.InvariantCulture);
        Debug.Assert(result is PyTypeObject);
        return (PyTypeObject)result;
    }
}

internal sealed partial class UserDefinedType<TObject> : PyTypeObject<TObject> where TObject : PyObject
{
    public override string Name { get; }
    public override IReadOnlyList<PyTypeObject> Bases { get; }
    internal override bool IsTypeImmutable => false;
    internal override bool IsImmutable => false;

    internal UserDefinedType(string name, string qualName, IReadOnlyList<PyTypeObject> bases) : base(name, bases, false)
    {
        Name = name;
        Bases = bases;
        PyAttributes.Add(PySpecialNames.QualName, PyStrObject.FromString(qualName));
    }
}

using PySharp.PyRuntime;
using PySharp.PyRuntime.PyAttributes;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PySharp.PyObjects.Builtins;

public abstract class PyTypeObject : PyObject, IPyObjectName
{
    public virtual IReadOnlyList<PyTypeObject> Bases => [PyBuiltinTypes.Object];
    public IReadOnlyList<PyTypeObject> MRO { get; }
    public abstract string Name { get; }
    public virtual string FullName => Name;
    public virtual string Document => string.Empty;
    

    internal PyTypeObject()
    {
        PyAttributes.Add(PySpecialNames.Bases, PyTupleObject.CreateTuple(Bases));
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        MRO = [.. EnumerableMROTypes(this, Bases)];
    }

    internal PyTypeObject(IReadOnlyList<PyTypeObject> bases, string name)
    {
        PyAttributes.Add(PySpecialNames.Bases, PyTupleObject.CreateTuple(bases));
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(name));
        MRO = [.. EnumerableMROTypes(this, bases)];
    }

    public bool IsInstance(PyObject obj)
    {
        return IsSubclass(obj.PyType);
    }
    public bool IsSubclass(PyTypeObject pyType)
    {
        foreach (var baseType in MRO)
        {
            if (baseType == this)
                return true;
        }

        return false;
    }

    public override PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pyObject = New(args, kwargs);
        if (pyObject is null)
            return null;
        if (pyObject.Init(args, kwargs) is null)
            return null;
        return pyObject;
    }

    public override PyObject? Repr()
    {
        return new PyStrObject($"<class '{Name}'>");
    }

    public static PyObject? PyTypeGetAttribute(PyTypeObject pyTypeObj, string name)
    {
        PyObject? attrFromType = null;
        foreach (var pyType in pyTypeObj.PyType.MRO)
        {
            if (pyType.PyAttributes.TryGetValue(name, out attrFromType))
                break;
        }

        PyObject? nonDataDescriptor = null;
        {
            if (attrFromType is not null && Utils.IsDescriptor(attrFromType, out var hasGet, out var hasSet, out var hasDelete))
            {
                if (hasGet)
                {
                    if (hasSet || hasDelete)
                        return attrFromType.Get(pyTypeObj, pyTypeObj.PyType);

                    nonDataDescriptor = attrFromType;
                }
            }
        }

        foreach (var pyType in pyTypeObj.MRO)
        {
            if (!pyType.PyAttributes.TryGetValue(name, out var attr))
                continue;

            if (Utils.IsDescriptor(attr, out var hasGet, out _, out _) && hasGet)
                return attr.Get(PyNoneObject.None, pyTypeObj);

            return attr;
        }

        if (nonDataDescriptor is not null)
            return nonDataDescriptor.Get(pyTypeObj, pyTypeObj.PyType);

        if (attrFromType is not null)
            return attrFromType;

        return PyVirtualMachine.RaiseAttributeError($"'{pyTypeObj.PyType.Name}' object has no attribute '{name}'");
    }

    public override PyObject? GetAttribute(string item)
    {
        return PyTypeGetAttribute(this, item);
    }

    public virtual PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError($"cannot create '{Name}' instances");
    }

    internal void AppendDefaultSpecialMethodsAsDescriptors<TPyObject>() where TPyObject : PyObject
    {
        AppendSpecialMethodAsDescriptor<TPyObject>(nameof(Repr), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptor<TPyObject>(nameof(Str), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptor<TPyObject>(nameof(Hash), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptor<TPyObject>(nameof(Bool), PySpecialMethodParametersType.NoArgs);
    }
    internal void AppendSpecialMethodsAsDescriptors<TPyObject>() where TPyObject : PyObject
    {
        AppendDefaultSpecialMethodsAsDescriptors<TPyObject>();
    }
    private static readonly FrozenDictionary<string, (string PyName, MethodInfo Method)> _nameToPyObjectMethod = new Dictionary<string, (string PyName, MethodInfo Method)>()
    {
        [nameof(Repr)] = (PySpecialNames.Repr, GetPublicMethodFromPyObjectNoCache(nameof(Repr))),
        [nameof(Str)] = (PySpecialNames.Str, GetPublicMethodFromPyObjectNoCache(nameof(Str))),
        [nameof(Hash)] = (PySpecialNames.Hash, GetPublicMethodFromPyObjectNoCache(nameof(Hash))),
        [nameof(Bool)] = (PySpecialNames.Bool, GetPublicMethodFromPyObjectNoCache(nameof(Bool))),
    }.ToFrozenDictionary();
    private static MethodInfo GetPublicMethodFromPyObjectNoCache(string name)
    {
        var method = typeof(PyObject).GetMethod(name);
        Debug.Assert(method is not null);
        return method;
    }

    private void AppendSpecialMethodAsDescriptorIfOverrided<TPyObject>(string methodName, PySpecialMethodParametersType paramType) where TPyObject : PyObject
    {
        if (!Utils.IsPyObjectMethodOverrided(typeof(TPyObject), methodName))
            return;

        var (pyName, method) = _nameToPyObjectMethod[methodName];
        PyAttributes[pyName] = new PyMethodDescriptorObject(pyName, method, paramType);
    }
    private void AppendSpecialMethodAsDescriptor<TPyObject>(string methodName, PySpecialMethodParametersType paramType) where TPyObject : PyObject
    {
        var (pyName, method) = _nameToPyObjectMethod[methodName];
        PyAttributes[pyName] = new PyMethodDescriptorObject(pyName, method, paramType);
    }
    internal void AppendMethodDescriptor<TPyObject>(string name, params string[] methodNames)
    {
        PyAttributes[name] = new PyMethodDescriptorObject(name, methodNames.Select(name =>
        {
            var method = typeof(TPyObject).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Assert(method is not null);
            return method;
        }));
    }

    private static IEnumerable<PyTypeObject> EnumerableMROTypes(PyTypeObject pyType, IEnumerable<PyTypeObject> bases)
    {
        // it is a simple implementation, instead of C3

        return bases.SelectMany(type => GetAllTypes(type)).Reverse().Distinct().Reverse().Prepend(pyType);

        static IEnumerable<PyTypeObject> GetAllTypes(PyTypeObject type)
        {
            yield return type;
            foreach (var baseType in type.Bases)
            {
                foreach (var mroType in GetAllTypes(baseType))
                {
                    yield return mroType;
                }
            }
        }
    }
}

public interface ISharedInstance<TSelf> where TSelf : ISharedInstance<TSelf>
{
    static abstract TSelf Shared { get; }
}

public abstract class PyTypeObject<TSelf> : PyTypeObject, ISharedInstance<TSelf> where TSelf : PyTypeObject<TSelf>, ISharedInstance<TSelf>, new()
{
    public static TSelf Shared { get; } = new TSelf();
}

public sealed class PyTypeObjectType : PyTypeObject
{
    public override string Name => "type";

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateCount(1, 0))
            return PyVirtualMachine.RaiseTypeError(null);

        return pack[0].PyType;
    }
}
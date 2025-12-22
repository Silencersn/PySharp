using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using PySharp.Utility;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Reflection;

namespace PySharp.PyModules.Builtins;

public abstract partial class PyTypeObject : PyObject, IPyObjectName
{
    public virtual IReadOnlyList<PyTypeObject> Bases => [PyObjectType.Shared];
    public IReadOnlyList<PyTypeObject> MRO { get; }
    public abstract string Name { get; }
    public virtual string FullName => Name; // TODO: FullName => <module_name>.Name
    public virtual string Document => string.Empty;
    public virtual bool IsSealed => false;
    public override PyTypeObject DefaultPyType => PyTypeObjectType.Shared;
    public abstract Type LayoutType { get; }
    internal virtual bool IsTypeImmutable => true;

    internal PyTypeObject()
    {
        MRO = [.. CreateMRO(this, Bases)];
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        PyAttributes.Add(PySpecialNames.Doc, PyNoneObject.None);
    }

    internal PyTypeObject(string name, IReadOnlyList<PyTypeObject> bases)
    {
        MRO = [.. CreateMRO(this, bases)];
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(name));
        PyAttributes.Add(PySpecialNames.Doc, PyNoneObject.None);
    }

    public bool IsInstance(PyObject obj)
    {
        return obj.PyType.IsSubclassOf(this);
    }

    public bool IsSubclassOf(PyTypeObject pyType)
    {
        foreach (var baseType in MRO)
        {
            if (baseType == pyType)
                return true;
        }

        return false;
    }

    protected internal override PyObject? CallImpl(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pyObject = New(this, args, kwargs);
        if (pyObject is null)
            return null;
        if (IsInstance(pyObject) && pyObject.Init(args, kwargs) is null)
            return null;
        return pyObject;
    }

    protected internal override PyObject? ReprImpl()
    {
        return PyStrObject.FromString($"<class '{Name}'>");
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

        return PyVirtualMachine.RaiseAttributeError($"'{pyTypeObj.Name}' object has no attribute '{name}'");
    }

    protected internal override PyObject? GetAttributeImpl(string item)
    {
        return PyTypeGetAttribute(this, item);
    }

    protected internal virtual PyObject? NewImpl(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // TODO: int.__new__(str): str is not a subtype of int
        return PyVirtualMachine.RaiseTypeError($"cannot create '{Name}' instances");
    }

    public PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!cls.IsSubclassOf(this))
            return PyVirtualMachine.RaiseTypeError($"{Name}.__new__({cls.Name}): {cls.Name} is not a subtype of {Name}");

        if (cls.LayoutType.IsSubclassOf(LayoutType))
            return PyVirtualMachine.RaiseTypeError($"{Name}.__new__({cls.Name}) is not safe, use {cls.Name}.__new__()");
        Debug.Assert(cls.LayoutType == LayoutType || LayoutType.IsSubclassOf(cls.LayoutType));

        var obj = NewImpl(cls, args, kwargs);
        if (obj is null)
            return null;
        obj._pyType = cls;
        return obj;
    }

    internal void AppendMemberDescriptor<TPyObject>(string name, Func<TPyObject, PyObject?> getter, Func<TPyObject, PyObject, PyObject?>? setter = null) where TPyObject : PyObject
    {
        PyAttributes[name] = new PyMemberDescriptorObject(
            (obj, _) =>
            {
                if (obj is not TPyObject pyObj)
                    return PyVirtualMachine.RaiseTypeError(null);

                return getter(pyObj);
            },
            setter is null ? null : (obj, value) =>
            {
                if (obj is not TPyObject pyObj)
                    return PyVirtualMachine.RaiseTypeError(null);

                return setter(pyObj, value);
            });
    }

    internal static void ValidateBases(IEnumerable<PyTypeObject> bases, out Type layoutType)
    {
        // TODO: check mro here

        layoutType = typeof(PyObject);
        foreach (var baseType in bases)
        {
            if (baseType.IsSealed)
            {
                PyVirtualMachine.RaiseTypeError($"type '{baseType.Name}' is not an acceptable base type");
                throw new PyRuntimeException(PyVirtualMachine.CurrentException);
            }

            if (baseType.LayoutType != layoutType)
            {
                if (baseType.LayoutType.IsSubclassOf(layoutType))
                {
                    layoutType = baseType.LayoutType;
                }
                else if (!layoutType.IsAssignableFrom(baseType.LayoutType))
                {
                    PyVirtualMachine.RaiseTypeError("multiple bases have instance lay-out conflict");
                    throw new PyRuntimeException(PyVirtualMachine.CurrentException);
                }
            }
        }
    }

    private static List<PyTypeObject> CreateMRO(PyTypeObject pyType, IEnumerable<PyTypeObject> bases)
    {
        // L[C(B1 ... BN)] = C + merge(L[B1] ... L[BN], B1 ... BN)
        List<PyTypeObject> resultMro = [pyType];

        // B1 ... BN
        var baseTypes = new Queue<PyTypeObject>(bases);
        if (baseTypes.Count is 0)
            // the type of object
            return resultMro;

        // L[B1] ... L[BN]
        List<Queue<PyTypeObject>> baseMros = [.. baseTypes.Select(baseType => new Queue<PyTypeObject>(baseType.MRO))];

        // L[B1] ... L[BN], B1 ... BN
        baseMros.Add(baseTypes);

        while (baseMros.Count > 0)
        {
            // take the head of the first list, i.e L[B1][0];
            // if this head is not in the tail of any of the other lists,
            // then add it to the linearization of C and remove it from the lists in the merge,
            // otherwise look at the head of the next list and take it, if it is a good head.
            //
            // Then repeat the operation until all the class are removed or it is impossible to find good heads.
            // In this case, it is impossible to construct the merge,
            // it will refuse to create the class C and will raise an exception.
            //
            for (int i = 0; i < baseMros.Count; i++)
            {
                var head = baseMros[i].Peek();
                bool notInOtherTails = true;
                for (int j = 0; j < baseMros.Count; j++)
                {
                    if (i == j)
                        continue;

                    var tail = baseMros[j].Skip(1);
                    if (tail.Contains(head))
                    {
                        notInOtherTails = false;
                        break;
                    }
                }
                if (notInOtherTails)
                {
                    resultMro.Add(head);
                    List<Queue<PyTypeObject>> baseMrosToRemove = [];
                    foreach (var baseMro in baseMros)
                    {
                        if (ReferenceEquals(baseMro.Peek(), head))
                        {
                            baseMro.Dequeue();
                            if (baseMro.Count is 0)
                                baseMrosToRemove.Add(baseMro);
                        }
                    }
                    foreach (var baseMroToRemove in baseMrosToRemove)
                    {
                        var removed = baseMros.Remove(baseMroToRemove);
                        Debug.Assert(removed);
                    }
                    break;
                }
                else if (i == baseMros.Count - 1)
                {
                    PyVirtualMachine.RaiseTypeError("Cannot create a consistent method resolution order (MRO)");
                    throw new PyRuntimeException(PyVirtualMachine.CurrentException);
                }
            }
        }

        return resultMro;
    }
}

public interface ISharedInstance<TSelf> where TSelf : ISharedInstance<TSelf>
{
    static abstract TSelf Shared { get; }
}


public sealed class PyTypeObjectType : PyTypeObject<PyTypeObjectType, PyTypeObject>
{
    public override string Name => "type";

    internal override bool IsImmutable => true;

    public PyTypeObjectType()
    {
        AppendMemberDescriptor<PyTypeObject>(PySpecialNames.Bases,
            static typeObj => PyTupleObject.CreateTuple(typeObj.Bases),
            static (typeObj, value) => throw new NotImplementedException());

        AppendMemberDescriptor<PyTypeObject>(PySpecialNames.Name,
            static typeObj => PyStrObject.FromString(typeObj.Name),
            static (typeObj, value) => throw new NotImplementedException());

        AppendMemberDescriptor<PyTypeObject>(PySpecialNames.MRO,
            static typeObj => PyTupleObject.CreateTuple(typeObj.MRO),
            static (typeObj, value) => throw new NotImplementedException());
    }

    protected internal override PyObject? NewImpl(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateCount(1, 0))
            return PyVirtualMachine.RaiseTypeError(null);

        return pack[0].PyType;
    }
}
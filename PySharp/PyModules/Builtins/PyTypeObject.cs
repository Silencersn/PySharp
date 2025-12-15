using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.Utility;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Reflection;

namespace PySharp.PyModules.Builtins;

public abstract class PyTypeObject : PyObject, IPyObjectName
{
    public virtual IReadOnlyList<PyTypeObject> Bases => [PyBuiltinTypes.Object];
    public IReadOnlyList<PyTypeObject> MRO { get; }
    public abstract string Name { get; }
    public virtual string FullName => Name; // TODO: FullName => <module_name>.Name
    public virtual string Document => string.Empty;
    public override PyTypeObject DefaultPyType => PyBuiltinTypes.Type;

    internal PyTypeObject()
    {
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        MRO = [.. CreateMRO(this, Bases)];
        PyAttributes.Add(PySpecialNames.Doc, PyNoneObject.None);
    }

    internal PyTypeObject(string name, IReadOnlyList<PyTypeObject> bases)
    {
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(name));
        MRO = [.. CreateMRO(this, bases)];
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

    public override PyObject? Call(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pyObject = New(this, args, kwargs);
        if (pyObject is null)
            return null;
        if (pyObject.Init(args, kwargs) is null)
            return null;
        return pyObject;
    }

    public override PyObject? Repr()
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

    public override PyObject? GetAttribute(string item)
    {
        return PyTypeGetAttribute(this, item);
    }

    public virtual PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return PyVirtualMachine.RaiseTypeError($"cannot create '{Name}' instances");
    }

    private void AppendSpecialMethodsAsDescriptors(IEnumerable<MethodInfo> methodInfos)
    {
        foreach (var methodInfo in methodInfos)
        {
            var (pyName, paramType) = _nameToPySpecialMethodParametersType[methodInfo.Name];

            // it is assumed that the key to be added does not exist in PyAttributes
            // if any key exists, it should be added in another way
            PyAttributes.Add(pyName, new PyMethodDescriptorObject(pyName, this, methodInfo, paramType));
        }
    }

    internal void AppendSpecialMethodsAsDescriptorsIfOverridden<TPyObject>() where TPyObject : PyObject
    {
        var names = _nameToPySpecialMethodParametersType.Keys.Where(name => Utils.IsPyObjectMethodOverridden(typeof(TPyObject), name));
        var methodInfos = NonVirtualCaller.Create<TPyObject>([.. names]);
        AppendSpecialMethodsAsDescriptors(methodInfos);
    }

    internal void AppendSpecialMethodsAsDescriptorsDirectly<TPyObject>(params string[] names) where TPyObject : PyObject
    {
        var methodInfos = NonVirtualCaller.Create<TPyObject>(names);
        AppendSpecialMethodsAsDescriptors(methodInfos);
    }

    private static readonly FrozenDictionary<string, (string PyName, PySpecialMethodParametersType ParamType)> _nameToPySpecialMethodParametersType =
        new Dictionary<string, (string, PySpecialMethodParametersType)>
        {
            [nameof(Repr)] = (PySpecialNames.Repr, PySpecialMethodParametersType.NoArgs),
            [nameof(Str)] = (PySpecialNames.Str, PySpecialMethodParametersType.NoArgs),
            [nameof(Hash)] = (PySpecialNames.Hash, PySpecialMethodParametersType.NoArgs),
            [nameof(Bool)] = (PySpecialNames.Bool, PySpecialMethodParametersType.NoArgs),
            [nameof(Int)] = (PySpecialNames.Int, PySpecialMethodParametersType.NoArgs),
            [nameof(Float)] = (PySpecialNames.Float, PySpecialMethodParametersType.NoArgs),
            [nameof(Complex)] = (PySpecialNames.Complex, PySpecialMethodParametersType.NoArgs),
            [nameof(Index)] = (PySpecialNames.Index, PySpecialMethodParametersType.NoArgs),
            [nameof(Call)] = (PySpecialNames.Call, PySpecialMethodParametersType.ArgsKwargs),
            [nameof(GetAttribute)] = (PySpecialNames.GetAttribute, PySpecialMethodParametersType.String),
            [nameof(GetAttr)] = (PySpecialNames.GetAttr, PySpecialMethodParametersType.String),
            [nameof(SetAttr)] = (PySpecialNames.SetAttr, PySpecialMethodParametersType.StringObject),
            [nameof(DelAttr)] = (PySpecialNames.DelAttr, PySpecialMethodParametersType.String),
            [nameof(Contains)] = (PySpecialNames.Contains, PySpecialMethodParametersType.Object),
            [nameof(GetItem)] = (PySpecialNames.GetItem, PySpecialMethodParametersType.Object),
            [nameof(SetItem)] = (PySpecialNames.SetItem, PySpecialMethodParametersType.ObjectObject),
            [nameof(DelItem)] = (PySpecialNames.DelItem, PySpecialMethodParametersType.Object),
            [nameof(Missing)] = (PySpecialNames.Missing, PySpecialMethodParametersType.Object),
            [nameof(Neg)] = (PySpecialNames.Neg, PySpecialMethodParametersType.NoArgs),
            [nameof(Pos)] = (PySpecialNames.Pos, PySpecialMethodParametersType.NoArgs),
            [nameof(Invert)] = (PySpecialNames.Invert, PySpecialMethodParametersType.NoArgs),
            [nameof(Abs)] = (PySpecialNames.Abs, PySpecialMethodParametersType.NoArgs),
            [nameof(Add)] = (PySpecialNames.Add, PySpecialMethodParametersType.Object),
            [nameof(Sub)] = (PySpecialNames.Sub, PySpecialMethodParametersType.Object),
            [nameof(Mul)] = (PySpecialNames.Mul, PySpecialMethodParametersType.Object),
            [nameof(TrueDiv)] = (PySpecialNames.TrueDiv, PySpecialMethodParametersType.Object),
            [nameof(FloorDiv)] = (PySpecialNames.FloorDiv, PySpecialMethodParametersType.Object),
            [nameof(Mod)] = (PySpecialNames.Mod, PySpecialMethodParametersType.Object),
            [nameof(DivMod)] = (PySpecialNames.DivMod, PySpecialMethodParametersType.Object),
            [nameof(Pow)] = (PySpecialNames.Pow, PySpecialMethodParametersType.ObjectObject),
            [nameof(LShift)] = (PySpecialNames.LShift, PySpecialMethodParametersType.Object),
            [nameof(RShift)] = (PySpecialNames.RShift, PySpecialMethodParametersType.Object),
            [nameof(And)] = (PySpecialNames.And, PySpecialMethodParametersType.Object),
            [nameof(Xor)] = (PySpecialNames.Xor, PySpecialMethodParametersType.Object),
            [nameof(Or)] = (PySpecialNames.Or, PySpecialMethodParametersType.Object),
            [nameof(RAdd)] = (PySpecialNames.RAdd, PySpecialMethodParametersType.Object),
            [nameof(RSub)] = (PySpecialNames.RSub, PySpecialMethodParametersType.Object),
            [nameof(RMul)] = (PySpecialNames.RMul, PySpecialMethodParametersType.Object),
            [nameof(RTrueDiv)] = (PySpecialNames.RTrueDiv, PySpecialMethodParametersType.Object),
            [nameof(RFloorDiv)] = (PySpecialNames.RFloorDiv, PySpecialMethodParametersType.Object),
            [nameof(RMod)] = (PySpecialNames.RMod, PySpecialMethodParametersType.Object),
            [nameof(RDivMod)] = (PySpecialNames.RDivMod, PySpecialMethodParametersType.Object),
            [nameof(RPow)] = (PySpecialNames.RPow, PySpecialMethodParametersType.ObjectObject),
            [nameof(RLShift)] = (PySpecialNames.RLShift, PySpecialMethodParametersType.Object),
            [nameof(RRShift)] = (PySpecialNames.RRShift, PySpecialMethodParametersType.Object),
            [nameof(RAnd)] = (PySpecialNames.RAnd, PySpecialMethodParametersType.Object),
            [nameof(RXor)] = (PySpecialNames.RXor, PySpecialMethodParametersType.Object),
            [nameof(ROr)] = (PySpecialNames.ROr, PySpecialMethodParametersType.Object),
            [nameof(Lt)] = (PySpecialNames.Lt, PySpecialMethodParametersType.Object),
            [nameof(Le)] = (PySpecialNames.Le, PySpecialMethodParametersType.Object),
            [nameof(Eq)] = (PySpecialNames.Eq, PySpecialMethodParametersType.Object),
            [nameof(Ne)] = (PySpecialNames.Ne, PySpecialMethodParametersType.Object),
            [nameof(Gt)] = (PySpecialNames.Gt, PySpecialMethodParametersType.Object),
            [nameof(Ge)] = (PySpecialNames.Ge, PySpecialMethodParametersType.Object),
            [nameof(Get)] = (PySpecialNames.Get, PySpecialMethodParametersType.ObjectObject),
            [nameof(Set)] = (PySpecialNames.Set, PySpecialMethodParametersType.ObjectObject),
            [nameof(Delete)] = (PySpecialNames.Delete, PySpecialMethodParametersType.Object),
            [nameof(Len)] = (PySpecialNames.Len, PySpecialMethodParametersType.NoArgs),
            [nameof(Iter)] = (PySpecialNames.Iter, PySpecialMethodParametersType.NoArgs),
            [nameof(Next)] = (PySpecialNames.Next, PySpecialMethodParametersType.NoArgs),
            [nameof(SetName)] = (PySpecialNames.SetName, PySpecialMethodParametersType.ObjectObject),
            [nameof(Init)] = (PySpecialNames.Init, PySpecialMethodParametersType.ArgsKwargs),
        }.ToFrozenDictionary();

    internal void AppendMethodDescriptor<TPyObject>(string name, params string[] methodNames)
    {
        PyAttributes[name] = new PyMethodDescriptorObject(name, this, methodNames.Select(name =>
        {
            var method = typeof(TPyObject).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Assert(method is not null);
            return method;
        }));
    }
    internal void AppendMethodDescriptor(string name, Delegate instanceDelegate, PySpecialMethodParametersType paramType)
    {
        PyAttributes[name] = new PyMethodDescriptorObject(name, this, instanceDelegate.Method, paramType);
    }
    internal void AppendMethodDescriptor(string name, MethodInfo instanceMethodInfo, PySpecialMethodParametersType paramType)
    {
        PyAttributes[name] = new PyMethodDescriptorObject(name, this, instanceMethodInfo, paramType);
    }
    internal void AppendMemberDescriptor(string name, Func<PyObject, PyObject, PyObject?> getter, Func<PyObject, PyObject, PyObject?>? setter = null)
    {
        PyAttributes[name] = new PyMemberDescriptorObject(getter, setter);
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
                        if (baseMro.Peek() == head)
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

public abstract class PyTypeObject<TSelf> : PyTypeObject, ISharedInstance<TSelf> where TSelf : PyTypeObject<TSelf>, ISharedInstance<TSelf>, new()
{
    public static TSelf Shared { get; } = new TSelf();
}

public abstract class PyPrimitiveTypeObject<TSelf, TObject> : PyTypeObject where TSelf : new() where TObject : PyObject
{
    public static TSelf Shared { get; } = new TSelf();

    private protected PyPrimitiveTypeObject()
    {
        AppendSpecialMethodsAsDescriptorsIfOverridden<TObject>();
    }
}

public sealed class PyTypeObjectType : PyPrimitiveTypeObject<PyTypeObjectType, PyTypeObject>
{
    public override string Name => "type";

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

    public override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateCount(1, 0))
            return PyVirtualMachine.RaiseTypeError(null);

        return pack[0].PyType;
    }
}
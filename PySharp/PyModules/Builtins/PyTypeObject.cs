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
    public override PyTypeObject PyType => PyBuiltinTypes.Type;

    internal PyTypeObject()
    {
        PyAttributes.Add(PySpecialNames.Bases, PyTupleObject.CreateTuple(Bases));
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        MRO = [.. CreateMRO(this, Bases)];
        PyAttributes.Add(PySpecialNames.MRO, PyTupleObject.CreateTuple(MRO));
    }

    internal PyTypeObject(string name, IReadOnlyList<PyTypeObject> bases)
    {
        PyAttributes.Add(PySpecialNames.Bases, PyTupleObject.CreateTuple(bases));
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(name));
        MRO = [.. CreateMRO(this, bases)];
        PyAttributes.Add(PySpecialNames.MRO, PyTupleObject.CreateTuple(MRO));
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

    internal void AppendOverriddenSpecialMethodsAsDescriptors<TPyObject>() where TPyObject : PyObject
    {
        var names = _nameToPyObjectMethod.Keys.Where(name => Utils.IsPyObjectMethodOverridden(typeof(TPyObject), name));
        var methodInfos = NonVirtualCaller.Create<TPyObject>([.. names]);
        foreach (var methodInfo in methodInfos)
        {
            var (pyName, paramType) = _nameToPySpecialMethodParametersType[methodInfo.Name];
            PyAttributes[pyName] = new PyMethodDescriptorObject(pyName, this, methodInfo, paramType);
        }
    }

    internal void AppendSpecialMethodsAsDescriptors<TPyObject>(params string[] names) where TPyObject : PyObject
    {
        var methodInfos = NonVirtualCaller.Create<TPyObject>(names);
        foreach (var methodInfo in methodInfos)
        {
            var (pyName, paramType) = _nameToPySpecialMethodParametersType[methodInfo.Name];
            PyAttributes[pyName] = new PyMethodDescriptorObject(pyName, this, methodInfo, paramType);
        }
    }

    [Obsolete("Use AppendOverriddenSpecialMethodsAsDescriptors or AppendSpecialMethodsAsDescriptors instead")]
    internal void AppendDefaultSpecialMethodsAsDescriptors()
    {
        AppendSpecialMethodAsDescriptor(nameof(Repr), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptor(nameof(Str), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptor(nameof(Hash), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptor(nameof(Bool), PySpecialMethodParametersType.NoArgs);
    }
    [Obsolete("Use AppendOverriddenSpecialMethodsAsDescriptors or AppendSpecialMethodsAsDescriptors instead")]
    internal void AppendSpecialMethodsAsDescriptors<TPyObject>() where TPyObject : PyObject
    {
        AppendDefaultSpecialMethodsAsDescriptors();

        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Add), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Sub), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Mul), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(DivMod), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Pow), PySpecialMethodParametersType.ObjectObject);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Mod), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Neg), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Pos), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Invert), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Abs), PySpecialMethodParametersType.NoArgs);

        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Eq), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Ne), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Lt), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Le), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Gt), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Ge), PySpecialMethodParametersType.Object);

        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(GetItem), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(SetItem), PySpecialMethodParametersType.ObjectObject);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(DelItem), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Contains), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Len), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Iter), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Next), PySpecialMethodParametersType.NoArgs);

        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(GetAttribute), PySpecialMethodParametersType.String);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(SetAttr), PySpecialMethodParametersType.StringObject);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(DelAttr), PySpecialMethodParametersType.String);

        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Call), PySpecialMethodParametersType.ArgsKwargs);

        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Int), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Float), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Complex), PySpecialMethodParametersType.NoArgs);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Index), PySpecialMethodParametersType.NoArgs);

        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(GetAttr), PySpecialMethodParametersType.String);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Missing), PySpecialMethodParametersType.Object);

        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(TrueDiv), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(FloorDiv), PySpecialMethodParametersType.Object);

        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(LShift), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RShift), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(And), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Xor), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Or), PySpecialMethodParametersType.Object);

        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RAdd), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RSub), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RMul), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RTrueDiv), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RFloorDiv), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RMod), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RDivMod), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RPow), PySpecialMethodParametersType.ObjectObject);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RLShift), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RRShift), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RAnd), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(RXor), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(ROr), PySpecialMethodParametersType.Object);

        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Get), PySpecialMethodParametersType.ObjectObject);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Set), PySpecialMethodParametersType.ObjectObject);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(Delete), PySpecialMethodParametersType.Object);
        AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(nameof(SetName), PySpecialMethodParametersType.ObjectObject);

    }
    [Obsolete("TODO: remove")]
    private static readonly FrozenDictionary<string, (string PyName, MethodInfo Method)> _nameToPyObjectMethod = new Dictionary<string, (string PyName, MethodInfo Method)>()
    {
        [nameof(Repr)] = (PySpecialNames.Repr, GetPublicMethodFromPyObjectNoCache(nameof(Repr))),
        [nameof(Str)] = (PySpecialNames.Str, GetPublicMethodFromPyObjectNoCache(nameof(Str))),
        [nameof(Hash)] = (PySpecialNames.Hash, GetPublicMethodFromPyObjectNoCache(nameof(Hash))),
        [nameof(Bool)] = (PySpecialNames.Bool, GetPublicMethodFromPyObjectNoCache(nameof(Bool))),
        [nameof(Int)] = (PySpecialNames.Int, GetPublicMethodFromPyObjectNoCache(nameof(Int))),
        [nameof(Float)] = (PySpecialNames.Float, GetPublicMethodFromPyObjectNoCache(nameof(Float))),
        [nameof(Complex)] = (PySpecialNames.Complex, GetPublicMethodFromPyObjectNoCache(nameof(Complex))),
        [nameof(Index)] = (PySpecialNames.Index, GetPublicMethodFromPyObjectNoCache(nameof(Index))),
        [nameof(Call)] = (PySpecialNames.Call, GetPublicMethodFromPyObjectNoCache(nameof(Call))),
        [nameof(GetAttribute)] = (PySpecialNames.GetAttribute, GetPublicMethodFromPyObjectNoCache(nameof(GetAttribute))),
        [nameof(GetAttr)] = (PySpecialNames.GetAttr, GetPublicMethodFromPyObjectNoCache(nameof(GetAttr))),
        [nameof(SetAttr)] = (PySpecialNames.SetAttr, GetPublicMethodFromPyObjectNoCache(nameof(SetAttr))),
        [nameof(DelAttr)] = (PySpecialNames.DelAttr, GetPublicMethodFromPyObjectNoCache(nameof(DelAttr))),
        [nameof(Contains)] = (PySpecialNames.Contains, GetPublicMethodFromPyObjectNoCache(nameof(Contains))),
        [nameof(GetItem)] = (PySpecialNames.GetItem, GetPublicMethodFromPyObjectNoCache(nameof(GetItem))),
        [nameof(SetItem)] = (PySpecialNames.SetItem, GetPublicMethodFromPyObjectNoCache(nameof(SetItem))),
        [nameof(DelItem)] = (PySpecialNames.DelItem, GetPublicMethodFromPyObjectNoCache(nameof(DelItem))),
        [nameof(Missing)] = (PySpecialNames.Missing, GetPublicMethodFromPyObjectNoCache(nameof(Missing))),
        [nameof(Neg)] = (PySpecialNames.Neg, GetPublicMethodFromPyObjectNoCache(nameof(Neg))),
        [nameof(Pos)] = (PySpecialNames.Pos, GetPublicMethodFromPyObjectNoCache(nameof(Pos))),
        [nameof(Invert)] = (PySpecialNames.Invert, GetPublicMethodFromPyObjectNoCache(nameof(Invert))),
        [nameof(Abs)] = (PySpecialNames.Abs, GetPublicMethodFromPyObjectNoCache(nameof(Abs))),
        [nameof(Add)] = (PySpecialNames.Add, GetPublicMethodFromPyObjectNoCache(nameof(Add))),
        [nameof(Sub)] = (PySpecialNames.Sub, GetPublicMethodFromPyObjectNoCache(nameof(Sub))),
        [nameof(Mul)] = (PySpecialNames.Mul, GetPublicMethodFromPyObjectNoCache(nameof(Mul))),
        [nameof(TrueDiv)] = (PySpecialNames.TrueDiv, GetPublicMethodFromPyObjectNoCache(nameof(TrueDiv))),
        [nameof(FloorDiv)] = (PySpecialNames.FloorDiv, GetPublicMethodFromPyObjectNoCache(nameof(FloorDiv))),
        [nameof(Mod)] = (PySpecialNames.Mod, GetPublicMethodFromPyObjectNoCache(nameof(Mod))),
        [nameof(DivMod)] = (PySpecialNames.DivMod, GetPublicMethodFromPyObjectNoCache(nameof(DivMod))),
        [nameof(Pow)] = (PySpecialNames.Pow, GetPublicMethodFromPyObjectNoCache(nameof(Pow))),
        [nameof(LShift)] = (PySpecialNames.LShift, GetPublicMethodFromPyObjectNoCache(nameof(LShift))),
        [nameof(RShift)] = (PySpecialNames.RShift, GetPublicMethodFromPyObjectNoCache(nameof(RShift))),
        [nameof(And)] = (PySpecialNames.And, GetPublicMethodFromPyObjectNoCache(nameof(And))),
        [nameof(Xor)] = (PySpecialNames.Xor, GetPublicMethodFromPyObjectNoCache(nameof(Xor))),
        [nameof(Or)] = (PySpecialNames.Or, GetPublicMethodFromPyObjectNoCache(nameof(Or))),
        [nameof(RAdd)] = (PySpecialNames.RAdd, GetPublicMethodFromPyObjectNoCache(nameof(RAdd))),
        [nameof(RSub)] = (PySpecialNames.RSub, GetPublicMethodFromPyObjectNoCache(nameof(RSub))),
        [nameof(RMul)] = (PySpecialNames.RMul, GetPublicMethodFromPyObjectNoCache(nameof(RMul))),
        [nameof(RTrueDiv)] = (PySpecialNames.RTrueDiv, GetPublicMethodFromPyObjectNoCache(nameof(RTrueDiv))),
        [nameof(RFloorDiv)] = (PySpecialNames.RFloorDiv, GetPublicMethodFromPyObjectNoCache(nameof(RFloorDiv))),
        [nameof(RMod)] = (PySpecialNames.RMod, GetPublicMethodFromPyObjectNoCache(nameof(RMod))),
        [nameof(RDivMod)] = (PySpecialNames.RDivMod, GetPublicMethodFromPyObjectNoCache(nameof(RDivMod))),
        [nameof(RPow)] = (PySpecialNames.RPow, GetPublicMethodFromPyObjectNoCache(nameof(RPow))),
        [nameof(RLShift)] = (PySpecialNames.RLShift, GetPublicMethodFromPyObjectNoCache(nameof(RLShift))),
        [nameof(RRShift)] = (PySpecialNames.RRShift, GetPublicMethodFromPyObjectNoCache(nameof(RRShift))),
        [nameof(RAnd)] = (PySpecialNames.RAnd, GetPublicMethodFromPyObjectNoCache(nameof(RAnd))),
        [nameof(RXor)] = (PySpecialNames.RXor, GetPublicMethodFromPyObjectNoCache(nameof(RXor))),
        [nameof(ROr)] = (PySpecialNames.ROr, GetPublicMethodFromPyObjectNoCache(nameof(ROr))),
        [nameof(Lt)] = (PySpecialNames.Lt, GetPublicMethodFromPyObjectNoCache(nameof(Lt))),
        [nameof(Le)] = (PySpecialNames.Le, GetPublicMethodFromPyObjectNoCache(nameof(Le))),
        [nameof(Eq)] = (PySpecialNames.Eq, GetPublicMethodFromPyObjectNoCache(nameof(Eq))),
        [nameof(Ne)] = (PySpecialNames.Ne, GetPublicMethodFromPyObjectNoCache(nameof(Ne))),
        [nameof(Gt)] = (PySpecialNames.Gt, GetPublicMethodFromPyObjectNoCache(nameof(Gt))),
        [nameof(Ge)] = (PySpecialNames.Ge, GetPublicMethodFromPyObjectNoCache(nameof(Ge))),
        [nameof(Get)] = (PySpecialNames.Get, GetPublicMethodFromPyObjectNoCache(nameof(Get))),
        [nameof(Set)] = (PySpecialNames.Set, GetPublicMethodFromPyObjectNoCache(nameof(Set))),
        [nameof(Delete)] = (PySpecialNames.Delete, GetPublicMethodFromPyObjectNoCache(nameof(Delete))),
        [nameof(Len)] = (PySpecialNames.Len, GetPublicMethodFromPyObjectNoCache(nameof(Len))),
        [nameof(Iter)] = (PySpecialNames.Iter, GetPublicMethodFromPyObjectNoCache(nameof(Iter))),
        [nameof(Next)] = (PySpecialNames.Next, GetPublicMethodFromPyObjectNoCache(nameof(Next))),
        [nameof(SetName)] = (PySpecialNames.SetName, GetPublicMethodFromPyObjectNoCache(nameof(SetName))),
    }.ToFrozenDictionary();

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
        }.ToFrozenDictionary();

    private static MethodInfo GetPublicMethodFromPyObjectNoCache(string name)
    {
        var method = typeof(PyObject).GetMethod(name);
        Debug.Assert(method is not null);
        return method;
    }

    [Obsolete("TODO: remove")]
    private void AppendSpecialMethodAsDescriptorIfOverridden<TPyObject>(string methodName, PySpecialMethodParametersType paramType) where TPyObject : PyObject
    {
        if (!Utils.IsPyObjectMethodOverridden(typeof(TPyObject), methodName))
            return;

        var (pyName, method) = _nameToPyObjectMethod[methodName];
        PyAttributes[pyName] = new PyMethodDescriptorObject(pyName, this, method, paramType);
    }
    [Obsolete("TODO: remove")]
    private void AppendSpecialMethodAsDescriptor(string methodName, PySpecialMethodParametersType paramType)
    {
        var (pyName, method) = _nameToPyObjectMethod[methodName];
        PyAttributes[pyName] = new PyMethodDescriptorObject(pyName, this, method, paramType);
    }
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
    internal void AppendMemberDescriptor<TPyObject>(string name, Func<TPyObject, PyObject?> getter) where TPyObject : PyObject
    {
        PyAttributes[name] = new PyMemberDescriptorObject(obj =>
        {
            if (obj is not TPyObject pyObj)
                return PyVirtualMachine.RaiseTypeError(null);

            return getter(pyObj);
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
        AppendSpecialMethodsAsDescriptors<TObject>();
    }
}

public sealed class PyTypeObjectType : PyTypeObject
{
    public override string Name => "type";

    public override PyObject? New(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (!pack.ValidateCount(1, 0))
            return PyVirtualMachine.RaiseTypeError(null);

        return pack[0].PyType;
    }
}
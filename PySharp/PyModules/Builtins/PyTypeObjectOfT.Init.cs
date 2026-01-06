using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PySharp.PyModules.Builtins;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods)]
partial class PyTypeObject<TObject>
{
    internal void AppendMemberDescriptor(string name, PyMemberGetter<TObject> getter, PyMemberSetter<TObject>? setter = null, PyMemberDeleter<TObject>? deleter = null)
    {
        PyAttributes[name] = new PyMemberDescriptorObject(this, getter.ToNonGeneric(), setter?.ToNonGeneric(), deleter?.ToNonGeneric());
    }

    internal void AppendMethodDescriptor(string name, params PyMethod<TObject>[] methods)
    {
        PyAttributes.Add(name, new PyMethodDescriptorObject(name, this, PyDelegateConverter.CreateOverloadDispatcher(methods)));
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
            [nameof(Format)] = (PySpecialNames.Format, PySpecialMethodParametersType.String),
        }.ToFrozenDictionary();

    internal void AppendSpecialMethodDescriptors(params ReadOnlySpan<string> names)
    {
        var type = GetType();
        foreach (var name in names)
        {
            var (pyName, paramType) = _nameToPySpecialMethodParametersType[name];

            var method = type
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(method => method.Name == name && method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject<TObject>));

            Debug.Assert(method is not null);

            PyAttributes.Add(pyName, new PyMethodDescriptorObject(pyName, this, method, paramType));
        }
    }

    private void AppendOverridenSpecialMethodDescriptors()
    {
        var type = GetType();
        foreach (var (name, (pyName, paramType)) in _nameToPySpecialMethodParametersType)
        {
            var method = type
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(method => method.Name == name && method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject<TObject>));

            Debug.Assert(method is not null);
            if (method.DeclaringType == typeof(PyTypeObject<TObject>))
                continue;

            PyAttributes.Add(pyName, new PyMethodDescriptorObject(pyName, this, method, paramType));
        }
    }

    private void AppendOverridenSpecialMethodDescriptors2()
    {
        AppendUnaryFunction(ref Slots.Repr, Repr);
        AppendUnaryFunction(ref Slots.Int, Int);
        AppendBinaryFunction(ref Slots.Add, Add);
        AppendBinaryFunction(ref Slots.RAdd, RAdd);

        bool IsOverriden(MethodInfo method)
        {
            var name = method.Name;
            // TODO: cache
            method = GetType()
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(method => method.Name == name && method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject<TObject>));
            return method.DeclaringType != typeof(PyTypeObject<TObject>);
        }

        void AppendUnaryFunction(ref PyUnaryFunction? field, PyUnaryFunction func)
        {
            if (IsOverriden(func.Method))
                field = func;
        }
        void AppendBinaryFunction(ref PyBinaryFunction? field, PyBinaryFunction func)
        {
            if (IsOverriden(func.Method))
                field = func;
        }
    }

    private void AppendNew()
    {
        var newMethod = GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(method => method.Name == "New" && method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject));
        if (newMethod.DeclaringType != typeof(PyTypeObject<TObject>))
        {
            var method = PyBuiltinFunctionOrMethodObject.CreateBoundMethodFromBound(PySpecialNames.New, this, null! /* TODO */, [PyFunctionArgsDef("cls", "*args", "**kwargs")] (context, arguments) =>
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
}

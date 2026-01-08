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
            [nameof(GetAttribute)] = (PySpecialNames.GetAttribute, PySpecialMethodParametersType.Object),
            [nameof(GetAttr)] = (PySpecialNames.GetAttr, PySpecialMethodParametersType.Object),
            [nameof(SetAttr)] = (PySpecialNames.SetAttr, PySpecialMethodParametersType.ObjectObject),
            [nameof(DelAttr)] = (PySpecialNames.DelAttr, PySpecialMethodParametersType.Object),
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

            //PyAttributes.Add(pyName, new PyMethodDescriptorObject(pyName, this, method, paramType));
        }
    }

    private void FillSlots()
    {
        FillSlot(PySpecialNames.Str, ref Slots.Str, Str);
        FillSlot(PySpecialNames.Repr, ref Slots.Repr, Repr);
        FillSlot(PySpecialNames.Bool, ref Slots.Bool, Bool);
        FillSlot(PySpecialNames.Hash, ref Slots.Hash, Hash);
        FillSlot(PySpecialNames.Len, ref Slots.Len, Len);
        FillSlot(PySpecialNames.Index, ref Slots.Index, Index);
        FillSlot(PySpecialNames.Int, ref Slots.Int, Int);
        FillSlot(PySpecialNames.Float, ref Slots.Float, Float);
        FillSlot(PySpecialNames.Call, ref Slots.Call, Call);

        FillSlot(PySpecialNames.Complex, ref Slots.Complex, Complex);
        FillSlot(PySpecialNames.Abs, ref Slots.Abs, Abs);
        FillSlot(PySpecialNames.Neg, ref Slots.Neg, Neg);
        FillSlot(PySpecialNames.Pos, ref Slots.Pos, Pos);
        FillSlot(PySpecialNames.Invert, ref Slots.Invert, Invert);
        FillSlot(PySpecialNames.SetName, ref Slots.SetName, SetName);
        FillSlot(PySpecialNames.Missing, ref Slots.Missing, Missing);
        FillSlot(PySpecialNames.Init, ref Slots.Init, Init);
        FillSlot(PySpecialNames.Format, ref Slots.Format, Format);

        FillSlot(PySpecialNames.Iter, ref Slots.Iter, Iter);
        FillSlot(PySpecialNames.Next, ref Slots.Next, Next);
        FillSlot(PySpecialNames.GetItem, ref Slots.GetItem, GetItem);
        FillSlot(PySpecialNames.SetItem, ref Slots.SetItem, SetItem);
        FillSlot(PySpecialNames.DelItem, ref Slots.DelItem, DelItem);
        FillSlot(PySpecialNames.Contains, ref Slots.Contains, Contains);

        FillSlot(PySpecialNames.Get, ref Slots.Get, Get);
        FillSlot(PySpecialNames.Set, ref Slots.Set, Set);
        FillSlot(PySpecialNames.Delete, ref Slots.Delete, Delete);
        FillSlot(PySpecialNames.GetAttribute, ref Slots.GetAttribute, GetAttribute);
        FillSlot(PySpecialNames.GetAttr, ref Slots.GetAttr, GetAttr);
        FillSlot(PySpecialNames.SetAttr, ref Slots.SetAttr, SetAttr);
        FillSlot(PySpecialNames.DelAttr, ref Slots.DelAttr, DelAttr);

        // Binary operators
        FillSlot(PySpecialNames.Add, ref Slots.Add, Add);
        FillSlot(PySpecialNames.Sub, ref Slots.Sub, Sub);
        FillSlot(PySpecialNames.Mul, ref Slots.Mul, Mul);
        FillSlot(PySpecialNames.TrueDiv, ref Slots.TrueDiv, TrueDiv);
        FillSlot(PySpecialNames.FloorDiv, ref Slots.FloorDiv, FloorDiv);
        FillSlot(PySpecialNames.Mod, ref Slots.Mod, Mod);
        FillSlot(PySpecialNames.DivMod, ref Slots.DivMod, DivMod);
        FillSlot(PySpecialNames.LShift, ref Slots.LShift, LShift);
        FillSlot(PySpecialNames.RShift, ref Slots.RShift, RShift);
        FillSlot(PySpecialNames.And, ref Slots.And, And);
        FillSlot(PySpecialNames.Xor, ref Slots.Xor, Xor);
        FillSlot(PySpecialNames.Or, ref Slots.Or, Or);

        // Reverse binary operators
        FillSlot(PySpecialNames.RAdd, ref Slots.RAdd, RAdd);
        FillSlot(PySpecialNames.RSub, ref Slots.RSub, RSub);
        FillSlot(PySpecialNames.RMul, ref Slots.RMul, RMul);
        FillSlot(PySpecialNames.RTrueDiv, ref Slots.RTrueDiv, RTrueDiv);
        FillSlot(PySpecialNames.RFloorDiv, ref Slots.RFloorDiv, RFloorDiv);
        FillSlot(PySpecialNames.RMod, ref Slots.RMod, RMod);
        FillSlot(PySpecialNames.RDivMod, ref Slots.RDivMod, RDivMod);
        FillSlot(PySpecialNames.RLShift, ref Slots.RLShift, RLShift);
        FillSlot(PySpecialNames.RRShift, ref Slots.RRShift, RRShift);
        FillSlot(PySpecialNames.RAnd, ref Slots.RAnd, RAnd);
        FillSlot(PySpecialNames.RXor, ref Slots.RXor, RXor);
        FillSlot(PySpecialNames.ROr, ref Slots.ROr, ROr);

        // Ternary operators
        FillSlot(PySpecialNames.Pow, ref Slots.Pow, Pow);
        FillSlot(PySpecialNames.RPow, ref Slots.RPow, RPow);

        // Rich comparison operators
        FillSlot(PySpecialNames.Lt, ref Slots.Lt, Lt);
        FillSlot(PySpecialNames.Le, ref Slots.Le, Le);
        FillSlot(PySpecialNames.Eq, ref Slots.Eq, Eq);
        FillSlot(PySpecialNames.Ne, ref Slots.Ne, Ne);
        FillSlot(PySpecialNames.Gt, ref Slots.Gt, Gt);
        FillSlot(PySpecialNames.Ge, ref Slots.Ge, Ge);

        bool IsOverriden(MethodInfo method)
        {
            var name = method.Name;
            // TODO: cache
            method = GetType()
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(method => method.Name == name && method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject<TObject>));
            return method.DeclaringType != typeof(PyTypeObject<TObject>);
        }

        void FillSlot<TDelegate>(string name, ref TDelegate? field, TDelegate func) where TDelegate : Delegate
        {
            if (IsOverriden(func.Method))
            {
                field = func;
                PyAttributes.Add(name, new PyWrapperDescriptorObject(func));
            }
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

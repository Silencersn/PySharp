using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PySharp.Modules.Builtins;

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

    // TODO: temp properties and methods, for testing source gen
    internal virtual bool UsingSourceGeneratedFillSlots => false;
    internal virtual void SourceGeneratedFillSlots()
    {

    }

    internal void FillSlot<TDelegate>(string name, ref TDelegate? field, TDelegate func) where TDelegate : Delegate
    {
        field = func;
        PyAttributes.Add(name, new PyWrapperDescriptorObject(func));
    }

    private void FillSlots()
    {
        if (UsingSourceGeneratedFillSlots)
        {
            SourceGeneratedFillSlots();
            return;
        }

        var type = GetType();
        var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
        var nameToMethod = methods
            .Where(static method => method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject<TObject>))
            .ToDictionary(static method => method.Name);

        FillSlotIsOverriden(PySpecialNames.Str, ref Slots.Str, Str);
        FillSlotIsOverriden(PySpecialNames.Repr, ref Slots.Repr, Repr);
        FillSlotIsOverriden(PySpecialNames.Bool, ref Slots.Bool, Bool);
        FillSlotIsOverriden(PySpecialNames.Hash, ref Slots.Hash, Hash);
        FillSlotIsOverriden(PySpecialNames.Len, ref Slots.Len, Len);
        FillSlotIsOverriden(PySpecialNames.Index, ref Slots.Index, Index);
        FillSlotIsOverriden(PySpecialNames.Int, ref Slots.Int, Int);
        FillSlotIsOverriden(PySpecialNames.Float, ref Slots.Float, Float);
        FillSlotIsOverriden(PySpecialNames.Call, ref Slots.Call, Call);

        FillSlotIsOverriden(PySpecialNames.Complex, ref Slots.Complex, Complex);
        FillSlotIsOverriden(PySpecialNames.Abs, ref Slots.Abs, Abs);
        FillSlotIsOverriden(PySpecialNames.Neg, ref Slots.Neg, Neg);
        FillSlotIsOverriden(PySpecialNames.Pos, ref Slots.Pos, Pos);
        FillSlotIsOverriden(PySpecialNames.Invert, ref Slots.Invert, Invert);
        FillSlotIsOverriden(PySpecialNames.SetName, ref Slots.SetName, SetName);
        FillSlotIsOverriden(PySpecialNames.Missing, ref Slots.Missing, Missing);
        FillSlotIsOverriden(PySpecialNames.Init, ref Slots.Init, Init);
        FillSlotIsOverriden(PySpecialNames.Format, ref Slots.Format, Format);

        FillSlotIsOverriden(PySpecialNames.Iter, ref Slots.Iter, Iter);
        FillSlotIsOverriden(PySpecialNames.Next, ref Slots.Next, Next);
        FillSlotIsOverriden(PySpecialNames.GetItem, ref Slots.GetItem, GetItem);
        FillSlotIsOverriden(PySpecialNames.SetItem, ref Slots.SetItem, SetItem);
        FillSlotIsOverriden(PySpecialNames.DelItem, ref Slots.DelItem, DelItem);
        FillSlotIsOverriden(PySpecialNames.Contains, ref Slots.Contains, Contains);

        FillSlotIsOverriden(PySpecialNames.Enter, ref Slots.Enter, Enter);
        FillSlotIsOverriden(PySpecialNames.Exit, ref Slots.Exit, Exit);

        FillSlotIsOverriden(PySpecialNames.Get, ref Slots.Get, Get);
        FillSlotIsOverriden(PySpecialNames.Set, ref Slots.Set, Set);
        FillSlotIsOverriden(PySpecialNames.Delete, ref Slots.Delete, Delete);
        FillSlotIsOverriden(PySpecialNames.GetAttribute, ref Slots.GetAttribute, GetAttribute);
        FillSlotIsOverriden(PySpecialNames.GetAttr, ref Slots.GetAttr, GetAttr);
        FillSlotIsOverriden(PySpecialNames.SetAttr, ref Slots.SetAttr, SetAttr);
        FillSlotIsOverriden(PySpecialNames.DelAttr, ref Slots.DelAttr, DelAttr);

        // Binary operators
        FillSlotIsOverriden(PySpecialNames.Add, ref Slots.Add, Add);
        FillSlotIsOverriden(PySpecialNames.Sub, ref Slots.Sub, Sub);
        FillSlotIsOverriden(PySpecialNames.Mul, ref Slots.Mul, Mul);
        FillSlotIsOverriden(PySpecialNames.TrueDiv, ref Slots.TrueDiv, TrueDiv);
        FillSlotIsOverriden(PySpecialNames.FloorDiv, ref Slots.FloorDiv, FloorDiv);
        FillSlotIsOverriden(PySpecialNames.Mod, ref Slots.Mod, Mod);
        FillSlotIsOverriden(PySpecialNames.DivMod, ref Slots.DivMod, DivMod);
        FillSlotIsOverriden(PySpecialNames.LShift, ref Slots.LShift, LShift);
        FillSlotIsOverriden(PySpecialNames.RShift, ref Slots.RShift, RShift);
        FillSlotIsOverriden(PySpecialNames.And, ref Slots.And, And);
        FillSlotIsOverriden(PySpecialNames.Xor, ref Slots.Xor, Xor);
        FillSlotIsOverriden(PySpecialNames.Or, ref Slots.Or, Or);

        // Reverse binary operators
        FillSlotIsOverriden(PySpecialNames.RAdd, ref Slots.RAdd, RAdd);
        FillSlotIsOverriden(PySpecialNames.RSub, ref Slots.RSub, RSub);
        FillSlotIsOverriden(PySpecialNames.RMul, ref Slots.RMul, RMul);
        FillSlotIsOverriden(PySpecialNames.RTrueDiv, ref Slots.RTrueDiv, RTrueDiv);
        FillSlotIsOverriden(PySpecialNames.RFloorDiv, ref Slots.RFloorDiv, RFloorDiv);
        FillSlotIsOverriden(PySpecialNames.RMod, ref Slots.RMod, RMod);
        FillSlotIsOverriden(PySpecialNames.RDivMod, ref Slots.RDivMod, RDivMod);
        FillSlotIsOverriden(PySpecialNames.RLShift, ref Slots.RLShift, RLShift);
        FillSlotIsOverriden(PySpecialNames.RRShift, ref Slots.RRShift, RRShift);
        FillSlotIsOverriden(PySpecialNames.RAnd, ref Slots.RAnd, RAnd);
        FillSlotIsOverriden(PySpecialNames.RXor, ref Slots.RXor, RXor);
        FillSlotIsOverriden(PySpecialNames.ROr, ref Slots.ROr, ROr);

        // Ternary operators
        FillSlotIsOverriden(PySpecialNames.Pow, ref Slots.Pow, Pow);
        FillSlotIsOverriden(PySpecialNames.RPow, ref Slots.RPow, RPow);

        // Rich comparison operators
        FillSlotIsOverriden(PySpecialNames.Lt, ref Slots.Lt, Lt);
        FillSlotIsOverriden(PySpecialNames.Le, ref Slots.Le, Le);
        FillSlotIsOverriden(PySpecialNames.Eq, ref Slots.Eq, Eq);
        FillSlotIsOverriden(PySpecialNames.Ne, ref Slots.Ne, Ne);
        FillSlotIsOverriden(PySpecialNames.Gt, ref Slots.Gt, Gt);
        FillSlotIsOverriden(PySpecialNames.Ge, ref Slots.Ge, Ge);

        // In-place binary operators
        FillSlotIsOverriden(PySpecialNames.IAdd, ref Slots.IAdd, IAdd);
        FillSlotIsOverriden(PySpecialNames.ISub, ref Slots.ISub, ISub);
        FillSlotIsOverriden(PySpecialNames.IMul, ref Slots.IMul, IMul);
        FillSlotIsOverriden(PySpecialNames.IMatMul, ref Slots.IMatMul, IMatMul);
        FillSlotIsOverriden(PySpecialNames.ITrueDiv, ref Slots.ITrueDiv, ITrueDiv);
        FillSlotIsOverriden(PySpecialNames.IFloorDiv, ref Slots.IFloorDiv, IFloorDiv);
        FillSlotIsOverriden(PySpecialNames.IMod, ref Slots.IMod, IMod);
        FillSlotIsOverriden(PySpecialNames.IPow, ref Slots.IPow, IPow);
        FillSlotIsOverriden(PySpecialNames.ILShift, ref Slots.ILShift, ILShift);
        FillSlotIsOverriden(PySpecialNames.IRShift, ref Slots.IRShift, IRShift);
        FillSlotIsOverriden(PySpecialNames.IAnd, ref Slots.IAnd, IAnd);
        FillSlotIsOverriden(PySpecialNames.IXor, ref Slots.IXor, IXor);
        FillSlotIsOverriden(PySpecialNames.IOr, ref Slots.IOr, IOr);

        bool IsOverriden(MethodInfo method)
        {
            var name = method.Name;
            return nameToMethod[name].DeclaringType != typeof(PyTypeObject<TObject>);
        }

        void FillSlotIsOverriden<TDelegate>(string name, ref TDelegate? field, TDelegate func) where TDelegate : Delegate
        {
            if (IsOverriden(func.Method))
                FillSlot(name, ref field, func);
        }
    }

    private void AppendNew()
    {
        var newMethod = GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(method => method.Name == nameof(New) && method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject));

        if (newMethod.DeclaringType == typeof(PyTypeObject<TObject>))
            return;

        Slots.New = New;

        var method = PyBuiltinFunctionOrMethodObject.CreateBoundMethodFromBound(PySpecialNames.New, this, null! /* TODO */, [PyFunctionArgsDef("cls", "*args", "**kwargs")] (context, arguments) =>
        {
            if (arguments[0] is not PyTypeObject cls)
                return PyResult.TypeError(PySR.Runtime_Type_NewClsNonType, FullName, arguments[0].PyType.FullName);

            if (!cls.IsSubclassOf(this))
                return PyResult.TypeError(PySR.Runtime_Type_NewClsNotSubtype, FullName, cls.FullName);

            if (cls.LayoutType.IsSubclassOf(LayoutType))
                return PyResult.TypeError(PySR.Runtime_Type_NewClsNotSafe, FullName, cls.FullName);
            Debug.Assert(cls.LayoutType == LayoutType || LayoutType.IsSubclassOf(cls.LayoutType));

            return New(context, cls, arguments.ExtraArgs, arguments.ExtraKwargs);
        });
        PyAttributes.Add(PySpecialNames.New, method);
    }
}

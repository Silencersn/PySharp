using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;
using System;
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

    private void FillSlots()
    {
        var type = GetType();
        var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
        var nameToMethod = methods
            .Where(static method => method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject<TObject>))
            .ToDictionary(static method => method.Name);

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
            return nameToMethod[name].DeclaringType != typeof(PyTypeObject<TObject>);
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
            .Single(method => method.Name == nameof(New) && method.GetBaseDefinition().DeclaringType == typeof(PyTypeObject));

        if (newMethod.DeclaringType == typeof(PyTypeObject<TObject>))
            return;

        Slots.New = New;

        var method = PyBuiltinFunctionOrMethodObject.CreateBoundMethodFromBound(PySpecialNames.New, this, null! /* TODO */, [PyFunctionArgsDef("cls", "*args", "**kwargs")] (context, arguments) =>
        {
            if (arguments[0] is not PyTypeObject cls)
                return PyResult.RaiseTypeError($"{FullName}.__new__(X): X is not a type object ({arguments[0].PyType.FullName})");

            if (!cls.IsSubclassOf(this))
                return PyResult.RaiseTypeError($"{FullName}.__new__({cls.FullName}): {cls.FullName} is not a subtype of {FullName}");

            if (cls.LayoutType.IsSubclassOf(LayoutType))
                return PyResult.RaiseTypeError($"{FullName}.__new__({cls.FullName}) is not safe, use {cls.FullName}.__new__()");
            Debug.Assert(cls.LayoutType == LayoutType || LayoutType.IsSubclassOf(cls.LayoutType));

            return New(context, cls, arguments.ExtraArgs, arguments.ExtraKwargs);
        });
        PyAttributes.Add(PySpecialNames.New, method);
    }
}

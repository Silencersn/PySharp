using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.ComponentModel;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject<TObject>
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendMemberDescriptor(string name, PyMemberGetter<TObject> getter, PyMemberSetter<TObject>? setter = null, PyMemberDeleter<TObject>? deleter = null, bool getSet = false)
    {
        // getSet models tp_getset (PyGetSetDef) rather than a READONLY
        // PyMemberDef, so the descriptor wears the getset_descriptor face
        PyTypeObject<PyMemberDescriptorObject> descriptorType = getSet ? PyGetSetDescriptorObjectType.Shared : PyMemberDescriptorObjectType.Shared;
        PyAttributes[name] = new PyMemberDescriptorObject(descriptorType, this, name, getter.ToNonGeneric(), setter?.ToNonGeneric(), deleter?.ToNonGeneric());
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendMethodDescriptor(string name, PyDelegateDefinition<PyMethod<TObject>> method)
    {
        var uncompoundedDelegate = method.ToUncompounded(name, QualName);
        PyAttributes[name] = new PyMethodDescriptorObject(name, this, uncompoundedDelegate);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendMethodDescriptor(string name, params PyDelegateDefinition<PyMethod<TObject>>[] methods)
    {
        var uncompoundedDelegate = methods.Length is 1 ? methods[0].ToUncompounded(name, QualName) : PyDelegateConverter.CreateOverloadDispatcher(name, QualName, methods);
        PyAttributes[name] = new PyMethodDescriptorObject(name, this, uncompoundedDelegate);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendClassMethod(string name, PyDelegateDefinition<PyMethod<PyTypeObject>> classMethod)
    {
        var uncompoundedDelegate = classMethod.ToUncompounded(name, QualName);
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, classMethod.ToUncompounded(name, QualName));
        PyAttributes[name] = new PyClassMethodObject(func);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendClassMethod(string name, params PyDelegateDefinition<PyMethod<PyTypeObject>>[] classMethods)
    {
        var uncompoundedDelegate = classMethods.Length is 1 ? classMethods[0].ToUncompounded(name, QualName) : PyDelegateConverter.CreateOverloadDispatcher(name, QualName, classMethods);
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, uncompoundedDelegate);
        PyAttributes[name] = new PyClassMethodObject(func);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendStaticMethod(string name, PyDelegateDefinition<PyFunction> staticMethod)
    {
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, staticMethod.ToUncompounded($"{QualName}.{name}"));
        PyAttributes[name] = new PyStaticMethodObject(func);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendStaticMethod(string name, params PyDelegateDefinition<PyFunction>[] staticMethods)
    {
        var uncompoundedDelegate = staticMethods.Length is 1 ? staticMethods[0].ToUncompounded($"{QualName}.{name}") : PyDelegateConverter.CreateOverloadDispatcher($"{QualName}.{name}", staticMethods);
        var func = PyBuiltinFunctionOrMethodObject.CreateFunction(name, uncompoundedDelegate);
        PyAttributes[name] = new PyStaticMethodObject(func);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void FillSlot<TDelegate>(string name, ref TDelegate? field, TDelegate func) where TDelegate : Delegate
    {
        field = func;
        PyAttributes[name] = new PyWrapperDescriptorObject(func);
    }

    // CPython add_operators: every non-null as_number slot exposes a
    // reflected view of the forward slot. The slot field mirrors
    // SLOT1BIN's dispatch step — PyOperators already swaps the operands,
    // so __r*(self=right operand, other=left operand) forwards as
    // (self, other) — while the own-dict wrapper mirrors
    // wrap_binaryfunc_r / wrap_ternaryfunc_r and flips them, because
    // attribute-level x.__rop__(y) means y op x. A hand-written R*
    // override keeps its slot, and a static builtin whose forward slot
    // is inherited from a base (slot_inherited) skips the own-dict
    // wrapper so attribute lookup resolves to the base's wrapper — bool
    // has no __radd__ of its own and picks up int's through the MRO.
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void FillReflectedSlots()
    {
        var number = Slots.Number;
        if (number is null)
            return;

        FillReflectedSlot(number, PySpecialNames.RAdd, static n => n.Add, static n => n.RAdd, static (n, f) => n.RAdd = f);
        FillReflectedSlot(number, PySpecialNames.RMul, static n => n.Mul, static n => n.RMul, static (n, f) => n.RMul = f);
        FillReflectedSlot(number, PySpecialNames.RSub, static n => n.Sub, static n => n.RSub, static (n, f) => n.RSub = f);
        FillReflectedSlot(number, PySpecialNames.RMatMul, static n => n.MatMul, static n => n.RMatMul, static (n, f) => n.RMatMul = f);
        FillReflectedSlot(number, PySpecialNames.RTrueDiv, static n => n.TrueDiv, static n => n.RTrueDiv, static (n, f) => n.RTrueDiv = f);
        FillReflectedSlot(number, PySpecialNames.RFloorDiv, static n => n.FloorDiv, static n => n.RFloorDiv, static (n, f) => n.RFloorDiv = f);
        FillReflectedSlot(number, PySpecialNames.RMod, static n => n.Mod, static n => n.RMod, static (n, f) => n.RMod = f);
        FillReflectedSlot(number, PySpecialNames.RDivMod, static n => n.DivMod, static n => n.RDivMod, static (n, f) => n.RDivMod = f);
        FillReflectedSlot(number, PySpecialNames.RLShift, static n => n.LShift, static n => n.RLShift, static (n, f) => n.RLShift = f);
        FillReflectedSlot(number, PySpecialNames.RRShift, static n => n.RShift, static n => n.RRShift, static (n, f) => n.RRShift = f);
        FillReflectedSlot(number, PySpecialNames.RAnd, static n => n.And, static n => n.RAnd, static (n, f) => n.RAnd = f);
        FillReflectedSlot(number, PySpecialNames.RXor, static n => n.Xor, static n => n.RXor, static (n, f) => n.RXor = f);
        FillReflectedSlot(number, PySpecialNames.ROr, static n => n.Or, static n => n.ROr, static (n, f) => n.ROr = f);


        if (number.RPow is null && number.Pow is not null && !IsInheritedPowSlot(number))
        {
            PyTernaryFunction powFunc = number.Pow;
            // same slot/wrapper split as the binary views: the dispatch
            // path swaps the operands, the attribute wrapper flips them
            PyResult Slot(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
                => self is TObject ? powFunc(context, self, other, modulo) : PyNotImplementedObject.NotImplemented;
            PyResult Attribute(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
                => other is TObject ? powFunc(context, other, self, modulo) : PyNotImplementedObject.NotImplemented;
            number.RPow = Slot;
            PyAttributes[PySpecialNames.RPow] = new PyWrapperDescriptorObject((PyTernaryFunction)Attribute);
        }
    }

    private void FillReflectedSlot(PyTypeSlots.PyNumberMethods number, string reflectedName, Func<PyTypeSlots.PyNumberMethods, PyBinaryFunction?> forward, Func<PyTypeSlots.PyNumberMethods, PyBinaryFunction?> reflectedGet, Action<PyTypeSlots.PyNumberMethods, PyBinaryFunction> setReflected)
    {
        // a hand-written override already wired the slot and its wrapper
        if (reflectedGet(number) is not null)
            return;
        // a static builtin inheriting the forward slot resolves the
        // reflected attribute through the base's dict (CPython
        // slot_inherited skip)
        if (IsInheritedForwardSlot(number, forward))
            return;

        var func = forward(number);
        if (func is null)
            return;

        PyBinaryFunction forwardFunc = func;
        // the dispatch path already swapped the operands, so the slot's
        // self is a right-side instance and forwards as (self, other);
        // the guard lets an inherited slot decline with NotImplemented
        // instead of letting the sealed bridge raise on a foreign self
        PyResult Slot(PyCallContext context, PyObject self, PyObject other)
            => self is TObject ? forwardFunc(context, self, other) : PyNotImplementedObject.NotImplemented;
        // CPython's forward C slot is order-agnostic (CHECK_BINOP checks
        // both operands), so wrap_binaryfunc_r can hand it the flipped
        // pair; the sealed bridge only accepts a TObject self, so the
        // flipped forwarding self is the caller's other
        PyResult Attribute(PyCallContext context, PyObject self, PyObject other)
            => other is TObject ? forwardFunc(context, other, self) : PyNotImplementedObject.NotImplemented;
        setReflected(number, Slot);
        // a method group's natural type is the matching System.Func, so
        // the wrapper delegate kind must be spelled out explicitly
        PyAttributes[reflectedName] = new PyWrapperDescriptorObject((PyBinaryFunction)Attribute);
    }

    // the forward delegate is inherited when the first MRO base with a
    // non-null slot holds the very same delegate reference (FillNullWith
    // copies by reference)
    private bool IsInheritedForwardSlot(PyTypeSlots.PyNumberMethods number, Func<PyTypeSlots.PyNumberMethods, PyBinaryFunction?> forward)
    {
        if (IsRuntimeCreated)
            return false;

        var func = forward(number);
        if (func is null)
            return false;

        for (int i = 1; i < InternalMRO.Length; i++)
        {
            var other = InternalMRO[i].Slots.Number;
            if (other is null)
                continue;
            var baseFunc = forward(other);
            if (baseFunc is not null)
                return ReferenceEquals(func, baseFunc);
        }
        return false;
    }

    private bool IsInheritedPowSlot(PyTypeSlots.PyNumberMethods number)
    {
        if (IsRuntimeCreated)
            return false;

        var powFunc = number.Pow;
        if (powFunc is null)
            return false;

        for (int i = 1; i < InternalMRO.Length; i++)
        {
            var other = InternalMRO[i].Slots.Number;
            if (other is null)
                continue;
            if (other.Pow is not null)
                return ReferenceEquals(powFunc, other.Pow);
        }
        return false;
    }

    protected virtual void FillSlots()
    {
    }

    protected virtual void RegisterMethods()
    {
    }

    protected virtual void RegisterProperties()
    {

    }

    /// <summary>
    /// Runs immediately before slot, method and property registration;
    /// identity, MRO and the type dict are already set up at this point.
    /// </summary>
    protected virtual void PreConstruct()
    {
    }

    /// <summary>
    /// Runs at the end of type construction, after slot, method and property
    /// registration. Type-dict customization belongs here rather than in a
    /// static constructor mutating <c>Shared</c> after its initializer.
    /// </summary>
    protected virtual void PostConstruct()
    {
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void FillNewSlot()
    {
        Slots.New = (context, cls, args, kwargs) =>
        {
            var validateResult = PyArgsValidator.ValidateNewCls(this, cls);
            if (validateResult.IsError)
                return validateResult;

            return New(context, cls, args, kwargs);
        };

        var method = PyBuiltinFunctionOrMethodObject.CreateBoundMethodFromBound(PySpecialNames.New, this, null! /* TODO */, (context, args, kwargs) =>
        {
            if (args.Count is 0)
                return PyResult.TypeError(PySR.Runtime_Type_New_NotEnoughArguments, TpName);

            if (args[0] is not PyTypeObject cls)
                return PyResult.TypeError(PySR.Runtime_Type_NewClsNonType, TpName, args[0].PyType.TpName);

            var validateResult = PyArgsValidator.ValidateNewCls(this, cls);
            if (validateResult.IsError)
                return validateResult;

            return New(context, cls, [.. args.Skip(1)], kwargs);
        });
        PyAttributes[PySpecialNames.New] = method;
    }
}

using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.ComponentModel;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject<TObject>
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void AppendMemberDescriptor(string name, PyMemberGetter<TObject> getter, PyMemberSetter<TObject>? setter = null, PyMemberDeleter<TObject>? deleter = null)
    {
        PyAttributes[name] = new PyMemberDescriptorObject(this, getter.ToNonGeneric(), setter?.ToNonGeneric(), deleter?.ToNonGeneric());
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
    // reflected wrapper that is a thin operand-swapping view of the
    // forward slot (wrap_binaryfunc_r / wrap_ternaryfunc_r keep the
    // ternary modulus in place). A hand-written R* override keeps its
    // slot, and a static builtin whose forward slot is inherited from a
    // base (slot_inherited) skips the own-dict wrapper so attribute
    // lookup resolves to the base's wrapper — bool has no __radd__ of
    // its own and picks up int's through the MRO.
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void FillReflectedSlots()
    {
        var number = Slots.Number;
        if (number is null)
            return;

        FillReflectedSlot(number, PySpecialNames.RAdd, static n => n.Add, static n => n.RAdd, static (n, f) => n.RAdd = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.RMul, static n => n.Mul, static n => n.RMul, static (n, f) => n.RMul = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.RSub, static n => n.Sub, static n => n.RSub, static (n, f) => n.RSub = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.RMatMul, static n => n.MatMul, static n => n.RMatMul, static (n, f) => n.RMatMul = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.RTrueDiv, static n => n.TrueDiv, static n => n.RTrueDiv, static (n, f) => n.RTrueDiv = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.RFloorDiv, static n => n.FloorDiv, static n => n.RFloorDiv, static (n, f) => n.RFloorDiv = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.RMod, static n => n.Mod, static n => n.RMod, static (n, f) => n.RMod = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.RDivMod, static n => n.DivMod, static n => n.RDivMod, static (n, f) => n.RDivMod = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.RLShift, static n => n.LShift, static n => n.RLShift, static (n, f) => n.RLShift = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.RRShift, static n => n.RShift, static n => n.RRShift, static (n, f) => n.RRShift = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.RAnd, static n => n.And, static n => n.RAnd, static (n, f) => n.RAnd = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.RXor, static n => n.Xor, static n => n.RXor, static (n, f) => n.RXor = f, swapsOperands: true);
        FillReflectedSlot(number, PySpecialNames.ROr, static n => n.Or, static n => n.ROr, static (n, f) => n.ROr = f, swapsOperands: true);


        if (number.RPow is null && number.Pow is not null && !IsInheritedPowSlot(number))
        {
            PyTernaryFunction powFunc = number.Pow;
            // same NotImplemented decline as the binary swapped views
            PyTernaryFunction reflected = (context, self, other, modulo) =>
                other is TObject ? powFunc(context, other, self, modulo) : PyNotImplementedObject.NotImplemented;
            number.RPow = reflected;
            PyAttributes[PySpecialNames.RPow] = new PyWrapperDescriptorObject(reflected);
        }
    }

    private void FillReflectedSlot(PyTypeSlots.PyNumberMethods number, string reflectedName, Func<PyTypeSlots.PyNumberMethods, PyBinaryFunction?> forward, Func<PyTypeSlots.PyNumberMethods, PyBinaryFunction?> reflectedGet, Action<PyTypeSlots.PyNumberMethods, PyBinaryFunction> setReflected, bool swapsOperands)
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
        // wrap_binaryfunc_r hands the reflected operand to the forward C
        // slot, which returns NotImplemented (never raises) on a type
        // mismatch — the sealed slot wrapper would raise instead, so the
        // swapped view checks the forwarded self and declines
        PyBinaryFunction reflected = swapsOperands
            ? (context, self, other) => other is TObject ? forwardFunc(context, other, self) : PyNotImplementedObject.NotImplemented
            : (context, self, other) => forwardFunc(context, self, other);
        setReflected(number, reflected);
        PyAttributes[reflectedName] = new PyWrapperDescriptorObject(reflected);
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

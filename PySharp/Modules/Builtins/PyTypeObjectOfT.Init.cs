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
    // reflected dict view (wrap_binaryfunc_r / wrap_ternaryfunc_r) — the
    // view flips the pair and hands it to the forward slot, running only
    // when the other operand carries this layout (a sealed bridge needs a
    // TObject self). A hand-written R* override instead exposes its own
    // dict view directly (FillReflectedView); a static builtin whose
    // forward slot is inherited from a base (slot_inherited) skips the
    // own-dict wrapper so attribute lookup resolves to the base's
    // wrapper — bool has no __radd__ of its own and picks up int's
    // through the MRO.
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void FillReflectedSlots()
    {
        var number = Slots.Number;
        if (number is null)
            return;

        FillReflectedSlot(number, PySpecialNames.RAdd, static n => n.Add);
        FillReflectedSlot(number, PySpecialNames.RMul, static n => n.Mul);
        FillReflectedSlot(number, PySpecialNames.RSub, static n => n.Sub);
        FillReflectedSlot(number, PySpecialNames.RMatMul, static n => n.MatMul);
        FillReflectedSlot(number, PySpecialNames.RTrueDiv, static n => n.TrueDiv);
        FillReflectedSlot(number, PySpecialNames.RFloorDiv, static n => n.FloorDiv);
        FillReflectedSlot(number, PySpecialNames.RMod, static n => n.Mod);
        FillReflectedSlot(number, PySpecialNames.RDivMod, static n => n.DivMod);
        FillReflectedSlot(number, PySpecialNames.RLShift, static n => n.LShift);
        FillReflectedSlot(number, PySpecialNames.RRShift, static n => n.RShift);
        FillReflectedSlot(number, PySpecialNames.RAnd, static n => n.And);
        FillReflectedSlot(number, PySpecialNames.RXor, static n => n.Xor);
        FillReflectedSlot(number, PySpecialNames.ROr, static n => n.Or);

        // same hand-written-override and inheritance yields as the binary
        // views (see FillReflectedSlot)
        if (number.Pow is not null
            && !PyAttributes.ContainsKey(PySpecialNames.RPow)
            && !IsInheritedPowSlot(number))
        {
            PyTernaryFunction powFunc = number.Pow;
            // same flip as the binary views; the modulo rides along
            // untouched
            PyResult Attribute(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
                => other is TObject otherOfT ? powFunc(context, otherOfT, self, modulo) : PyNotImplementedObject.NotImplemented;
            PyAttributes[PySpecialNames.RPow] = new PyWrapperDescriptorObject((PyTernaryFunction)Attribute);
        }
    }

    private void FillReflectedSlot(PyTypeSlots.PyNumberMethods number, string reflectedName, Func<PyTypeSlots.PyNumberMethods, PyBinaryFunction?> forward)
    {
        // a hand-written override already wired its own dict view
        // (FillReflectedView runs earlier in FillSlots)
        if (PyAttributes.ContainsKey(reflectedName))
            return;
        // no forward slot, no synthesized view
        if (forward(number) is null)
            return;
        // a static builtin inheriting the forward slot resolves the
        // reflected attribute through the base's dict (CPython
        // slot_inherited skip)
        if (IsInheritedForwardSlot(number, forward))
            return;

        PyBinaryFunction forwardFunc = forward(number)!;
        PyResult Attribute(PyCallContext context, PyObject self, PyObject other)
            => other is TObject otherOfT ? forwardFunc(context, otherOfT, self) : PyNotImplementedObject.NotImplemented;
        PyAttributes[reflectedName] = new PyWrapperDescriptorObject((PyBinaryFunction)Attribute);
    }

    // The dict view for a hand-written R* override: x.__r*(y) answers the
    // override's own (x, y) directly — the dispatch path reaches the same
    // body through the forward bridge's flipped fallback, so both faces
    // stay one implementation. Generated into FillSlots for every [PySlot]
    // marked R* override (the reflected names have no slot fields of
    // their own).
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void FillReflectedView(string name, Func<PyCallContext, TObject, PyObject, PyResult> reflectedVirtual)
    {
        PyResult Attribute(PyCallContext context, PyObject self, PyObject other)
            => self is TObject selfOfT ? reflectedVirtual(context, selfOfT, other) : PyNotImplementedObject.NotImplemented;
        PyAttributes[name] = new PyWrapperDescriptorObject((PyBinaryFunction)Attribute);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void FillReflectedView(string name, Func<PyCallContext, TObject, PyObject, PyObject, PyResult> reflectedVirtual)
    {
        PyResult Attribute(PyCallContext context, PyObject self, PyObject other, PyObject modulo)
            => self is TObject selfOfT ? reflectedVirtual(context, selfOfT, other, modulo) : PyNotImplementedObject.NotImplemented;
        PyAttributes[name] = new PyWrapperDescriptorObject((PyTernaryFunction)Attribute);
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

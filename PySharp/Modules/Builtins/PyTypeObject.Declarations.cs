using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

partial class PyTypeObject
{
    /// <summary>
    /// Serves as a metadata manifest for Python's special methods (dunder methods / slots).
    /// 
    /// The Roslyn incremental source generator (InternalPyTypeObjectGenerator) analyzes the structure
    /// of this class to automatically inject boilerplate code across the project. 
    /// Specifically, it generates:
    /// <list type="bullet">
    /// <item>Base virtual method definitions in <c>PyTypeObject</c>, designed to be overridden by typed wrappers.</item>
    /// <item>Strongly-typed sealed wrappers in <c>PyTypeObject&lt;TObject&gt;</c> that handle type checking of <c>self</c>.</item>
    /// <item>
    /// Slot delegate fields in <c>PyTypeObject.PyTypeSlots</c>, as well as utility methods:
    /// <list type="bullet">
    /// <item><c>FillNullWith(other)</c>: Populates missing (null) slots with implementations from a base type (used in MRO).</item>
    /// <item><c>TrySetSlot(name, value)</c>: Dynamically sets a slot (converting a <c>PyObject</c> to the required delegate type) based on its special method name.</item>
    /// <item><c>ClearSlot(name)</c>: Nulls a slot by special method name — the outcome of resolving with no dict provider anywhere in the MRO.</item>
    /// <item><c>TrySetWrappedSlot(name, wrapper)</c>: Wires a slot from a wrapper descriptor's exact delegate when the delegate type matches the name.</item>
    /// <item><c>AllSlotNames</c> / <c>IsSlotName(name)</c>: The full special-method name list and membership test driving slot re-resolution (creation fixup and mutation propagation).</item>
    /// </list>
    /// </item>
    /// <item>Constant string aliases in <c>PySpecialNames</c> (e.g. <c>public const string Add = "__add__";</c>).</item>
    /// </list>
    /// 
    /// <b>Note on Implementation Details:</b>
    /// Methods that do not consume an instance <c>self</c> as their first parameter (such as <c>__new__</c>, which receives <c>cls</c>) 
    /// <b>are not defined here</b>. Because they don't fit the generic <c>TObject</c> signature pattern, their slot fields,
    /// and constants (e.g., <c>PyTypeSlots.New</c>, <c>PySpecialNames.New</c>) are managed manually. However, the generated
    /// <c>TrySetSlot</c>, <c>ClearSlot</c> and <c>TrySetWrappedSlot</c> methods still include hardcoded switch-case branches for
    /// <c>__new__</c> for integration convenience (the last one returns false, preserving <c>FillNewSlot</c>'s validation closure).
    /// </summary>
    private static partial class Declarations
    {
        /// <summary>
        /// Annotates a partial method declaration to bind it to a Python special method name and its corresponding delegate type.
        /// </summary>
        /// <param name="name">The python magic name, e.g., "__init__".</param>
        /// <param name="delegateType">The delegate type handling the call, e.g., typeof(PyUnaryFunction).</param>
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
#pragma warning disable CS9113
        private sealed class PySpecialMethodAttribute(string name, Type delegateType) : PyAttribute
        {
            public string? SlotsMember { get; set; }
        }
#pragma warning restore CS9113

        /// <summary>
        /// A placeholder type used in the partial method signatures to represent the 'self' instance.
        /// The source generator treats parameters of this type dynamically: it targets <c>PyObject</c> in 
        /// the base <c>PyTypeObject</c>, and the generic <c>TObject</c> param in <c>PyTypeObject&lt;TObject&gt;</c>.
        /// </summary>
        private sealed class TObject : PyObject;

#pragma warning disable IDE0051
#pragma warning disable IDE0060
#pragma warning disable PYSPI001

        [PySpecialMethod("__init__", typeof(PySelfArgsKwargsFunction))]
        static partial void Init(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);

        [PySpecialMethod("__call__", typeof(PySelfArgsKwargsFunction))]
        static partial void Call(PyCallContext context, TObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs);

        [PySpecialMethod("__repr__", typeof(PyUnaryFunction))]
        static partial void Repr(PyCallContext context, TObject self);

        [PySpecialMethod("__str__", typeof(PyUnaryFunction))]
        static partial void Str(PyCallContext context, TObject self);

        [PySpecialMethod("__hash__", typeof(PyUnaryFunction))]
        static partial void Hash(PyCallContext context, TObject self);

        [PySpecialMethod("__getattribute__", typeof(PyBinaryFunction))]
        static partial void GetAttribute(PyCallContext context, TObject self, PyObject item);

        [PySpecialMethod("__getattr__", typeof(PyBinaryFunction))]
        static partial void GetAttr(PyCallContext context, TObject self, PyObject item);

        [PySpecialMethod("__setattr__", typeof(PyTernaryFunction))]
        static partial void SetAttr(PyCallContext context, TObject self, PyObject key, PyObject value);

        [PySpecialMethod("__delattr__", typeof(PyBinaryFunction))]
        static partial void DelAttr(PyCallContext context, TObject self, PyObject item);

        [PySpecialMethod("__bool__", typeof(PyUnaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Bool(PyCallContext context, TObject self);

        [PySpecialMethod("__int__", typeof(PyUnaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Int(PyCallContext context, TObject self);

        [PySpecialMethod("__float__", typeof(PyUnaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Float(PyCallContext context, TObject self);

        [PySpecialMethod("__index__", typeof(PyUnaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Index(PyCallContext context, TObject self);

        [PySpecialMethod("__contains__", typeof(PyBinaryFunction))]
        static partial void Contains(PyCallContext context, TObject self, PyObject item);

        [PySpecialMethod("__getitem__", typeof(PyBinaryFunction))]
        static partial void GetItem(PyCallContext context, TObject self, PyObject item);

        [PySpecialMethod("__setitem__", typeof(PyTernaryFunction))]
        static partial void SetItem(PyCallContext context, TObject self, PyObject key, PyObject value);

        [PySpecialMethod("__delitem__", typeof(PyBinaryFunction))]
        static partial void DelItem(PyCallContext context, TObject self, PyObject key);

        [PySpecialMethod("__len__", typeof(PyUnaryFunction))]
        static partial void Len(PyCallContext context, TObject self);

        [PySpecialMethod("__iter__", typeof(PyUnaryFunction))]
        static partial void Iter(PyCallContext context, TObject self);

        [PySpecialMethod("__next__", typeof(PyUnaryFunction))]
        static partial void Next(PyCallContext context, TObject self);

        [PySpecialMethod("__neg__", typeof(PyUnaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Neg(PyCallContext context, TObject self);

        [PySpecialMethod("__pos__", typeof(PyUnaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Pos(PyCallContext context, TObject self);

        [PySpecialMethod("__invert__", typeof(PyUnaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Invert(PyCallContext context, TObject self);

        [PySpecialMethod("__abs__", typeof(PyUnaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Abs(PyCallContext context, TObject self);

        [PySpecialMethod("__add__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Add(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__sub__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Sub(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__mul__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Mul(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__matmul__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void MatMul(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__truediv__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void TrueDiv(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__floordiv__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void FloorDiv(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__mod__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Mod(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__divmod__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void DivMod(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__pow__", typeof(PyTernaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Pow(PyCallContext context, TObject self, PyObject other, PyObject modulo);

        [PySpecialMethod("__lshift__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void LShift(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rshift__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void RShift(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__and__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void And(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__xor__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Xor(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__or__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void Or(PyCallContext context, TObject self, PyObject other);

        // The reflected arithmetic dunders (__radd__ .. __ror__) left this
        // manifest in the slot layering rework: like CPython's SLOT1BINFULL
        // slot_nb_add, the reflection protocol lives inside the forward
        // slot's bridge (forward guard, then the reflected fallback with
        // the pair flipped), never in dedicated r* slot fields. The
        // type-safe R* virtual methods stay on PyTypeObject<TObject> as
        // plain virtuals (no [PySpecialMethod], no generated bridges).

        // The 13 wide dunders (__complex__ .. __ceil__, the CPython names
        // no slotdef carries) left the manifest too: their protocol entry
        // points resolve the name on the type's MRO at call time
        // (_PyObject_LookupSpecial), so the type dict is the single source
        // of truth and the binding follows the descriptor protocol instead
        // of being frozen at slot-wiring time. The wide virtuals stay on
        // PyTypeObject<TObject> as [PySlot]-marked dict-only views
        // (PyTypeGenerator.WideDictSlotNames).

        [PySpecialMethod("__get__", typeof(PyTernaryFunction))]
        static partial void Get(PyCallContext context, TObject self, PyObject instance, PyObject owner);

        [PySpecialMethod("__set__", typeof(PyTernaryFunction))]
        static partial void Set(PyCallContext context, TObject self, PyObject instance, PyObject value);

        [PySpecialMethod("__delete__", typeof(PyBinaryFunction))]
        static partial void Delete(PyCallContext context, TObject self, PyObject instance);

        [PySpecialMethod("__iadd__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void IAdd(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__isub__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void ISub(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__imul__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void IMul(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__imatmul__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void IMatMul(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__itruediv__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void ITrueDiv(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__ifloordiv__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void IFloorDiv(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__imod__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void IMod(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__ipow__", typeof(PyTernaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void IPow(PyCallContext context, TObject self, PyObject other, PyObject modulo);

        [PySpecialMethod("__ilshift__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void ILShift(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__irshift__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void IRShift(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__iand__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void IAnd(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__ixor__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void IXor(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__ior__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Number))]
        static partial void IOr(PyCallContext context, TObject self, PyObject other);

        // CPython's sq_concat/sq_repeat (and their in-place variants): the
        // sequence-side slots of __add__/__mul__ — one dunder name mapping to
        // slots in two protocol families. Native sequence types fill only the
        // sq side; a heap type defining __add__/__mul__ keeps the sq slot
        // NULL (slotdef function=NULL, typeobject.c:11131) and the abstract
        // layer falls back from nb to sq.
        [PySpecialMethod("__add__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Sequence))]
        static partial void Concat(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__mul__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Sequence))]
        static partial void Repeat(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__iadd__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Sequence))]
        static partial void InplaceConcat(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__imul__", typeof(PyBinaryFunction), SlotsMember = nameof(PyTypeSlots.Sequence))]
        static partial void InplaceRepeat(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__await__", typeof(PyUnaryFunction))]
        static partial void Await(PyCallContext context, TObject self);

        [PySpecialMethod("__aiter__", typeof(PyUnaryFunction))]
        static partial void AIter(PyCallContext context, TObject self);

        [PySpecialMethod("__anext__", typeof(PyUnaryFunction))]
        static partial void ANext(PyCallContext context, TObject self);

        [PySpecialMethod("__buffer__", typeof(PyBufferFunction))]
        static partial void Buffer(PyCallContext context, TObject self, int flags);

        [PySpecialMethod("__release_buffer__", typeof(PyReleaseBufferFunction))]
        static partial void ReleaseBuffer(PyCallContext context, TObject self, PyObject buffer);

#pragma warning restore PYSPI001
#pragma warning restore IDE0060
#pragma warning restore IDE0051
    }
}

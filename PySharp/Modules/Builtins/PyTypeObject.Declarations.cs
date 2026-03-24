using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

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
    /// <item><c>Clone()</c>: Creates a shallow copy of the slots structure.</item>
    /// <item><c>FillNullWith(other)</c>: Populates missing (null) slots with implementations from a base type (used in MRO).</item>
    /// <item><c>TrySetSlot(name, value)</c>: Dynamically sets a slot (converting a <c>PyObject</c> to the required delegate type) based on its special method name.</item>
    /// </list>
    /// </item>
    /// <item>Constant string aliases in <c>PySpecialNames</c> (e.g. <c>public const string Add = "__add__";</c>).</item>
    /// </list>
    /// 
    /// <b>Note on Implementation Details:</b>
    /// Methods that do not consume an instance <c>self</c> as their first parameter (such as <c>__new__</c>, which receives <c>cls</c>) 
    /// <b>are not defined here</b>. Because they don't fit the generic <c>TObject</c> signature pattern, their slot fields, 
    /// and constants (e.g., <c>PyTypeSlots.New</c>, <c>PySpecialNames.New</c>) are managed manually. However, the generated 
    /// <c>TrySetSlot</c> method still includes a hardcoded switch-case branch for <c>__new__</c> for integration convenience.
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
        private sealed class PySpecialMethodAttribute(string name, Type delegateType) : PyAttribute;
#pragma warning restore CS9113

        /// <summary>
        /// A placeholder type used in the partial method signatures to represent the 'self' instance.
        /// The source generator treats parameters of this type dynamically: it targets <c>PyObject</c> in 
        /// the base <c>PyTypeObject</c>, and the generic <c>TObject</c> param in <c>PyTypeObject&lt;TObject&gt;</c>.
        /// </summary>
        private sealed class TObject : PyObject;

#pragma warning disable IDE0051
#pragma warning disable IDE0060

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

        [PySpecialMethod("__bool__", typeof(PyUnaryFunction))]
        static partial void Bool(PyCallContext context, TObject self);

        [PySpecialMethod("__int__", typeof(PyUnaryFunction))]
        static partial void Int(PyCallContext context, TObject self);

        [PySpecialMethod("__float__", typeof(PyUnaryFunction))]
        static partial void Float(PyCallContext context, TObject self);

        [PySpecialMethod("__complex__", typeof(PyUnaryFunction))]
        static partial void Complex(PyCallContext context, TObject self);

        [PySpecialMethod("__index__", typeof(PyUnaryFunction))]
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

        [PySpecialMethod("__neg__", typeof(PyUnaryFunction))]
        static partial void Neg(PyCallContext context, TObject self);

        [PySpecialMethod("__pos__", typeof(PyUnaryFunction))]
        static partial void Pos(PyCallContext context, TObject self);

        [PySpecialMethod("__invert__", typeof(PyUnaryFunction))]
        static partial void Invert(PyCallContext context, TObject self);

        [PySpecialMethod("__abs__", typeof(PyUnaryFunction))]
        static partial void Abs(PyCallContext context, TObject self);

        [PySpecialMethod("__add__", typeof(PyBinaryFunction))]
        static partial void Add(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__sub__", typeof(PyBinaryFunction))]
        static partial void Sub(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__mul__", typeof(PyBinaryFunction))]
        static partial void Mul(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__matmul__", typeof(PyBinaryFunction))]
        static partial void MatMul(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__truediv__", typeof(PyBinaryFunction))]
        static partial void TrueDiv(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__floordiv__", typeof(PyBinaryFunction))]
        static partial void FloorDiv(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__mod__", typeof(PyBinaryFunction))]
        static partial void Mod(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__divmod__", typeof(PyBinaryFunction))]
        static partial void DivMod(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__pow__", typeof(PyTernaryFunction))]
        static partial void Pow(PyCallContext context, TObject self, PyObject other, PyObject modulo);

        [PySpecialMethod("__lshift__", typeof(PyBinaryFunction))]
        static partial void LShift(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rshift__", typeof(PyBinaryFunction))]
        static partial void RShift(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__and__", typeof(PyBinaryFunction))]
        static partial void And(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__xor__", typeof(PyBinaryFunction))]
        static partial void Xor(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__or__", typeof(PyBinaryFunction))]
        static partial void Or(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__radd__", typeof(PyBinaryFunction))]
        static partial void RAdd(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rsub__", typeof(PyBinaryFunction))]
        static partial void RSub(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rmul__", typeof(PyBinaryFunction))]
        static partial void RMul(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rmatmul__", typeof(PyBinaryFunction))]
        static partial void RMatMul(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rtruediv__", typeof(PyBinaryFunction))]
        static partial void RTrueDiv(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rfloordiv__", typeof(PyBinaryFunction))]
        static partial void RFloorDiv(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rmod__", typeof(PyBinaryFunction))]
        static partial void RMod(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rdivmod__", typeof(PyBinaryFunction))]
        static partial void RDivMod(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rpow__", typeof(PyTernaryFunction))]
        static partial void RPow(PyCallContext context, TObject self, PyObject other, PyObject modulo);

        [PySpecialMethod("__rlshift__", typeof(PyBinaryFunction))]
        static partial void RLShift(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rrshift__", typeof(PyBinaryFunction))]
        static partial void RRShift(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rand__", typeof(PyBinaryFunction))]
        static partial void RAnd(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__rxor__", typeof(PyBinaryFunction))]
        static partial void RXor(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__ror__", typeof(PyBinaryFunction))]
        static partial void ROr(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__lt__", typeof(PyBinaryFunction))]
        static partial void Lt(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__le__", typeof(PyBinaryFunction))]
        static partial void Le(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__eq__", typeof(PyBinaryFunction))]
        static partial void Eq(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__ne__", typeof(PyBinaryFunction))]
        static partial void Ne(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__gt__", typeof(PyBinaryFunction))]
        static partial void Gt(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__ge__", typeof(PyBinaryFunction))]
        static partial void Ge(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__missing__", typeof(PyBinaryFunction))]
        static partial void Missing(PyCallContext context, TObject self, PyObject key);

        [PySpecialMethod("__get__", typeof(PyTernaryFunction))]
        static partial void Get(PyCallContext context, TObject self, PyObject instance, PyObject owner);

        [PySpecialMethod("__set__", typeof(PyTernaryFunction))]
        static partial void Set(PyCallContext context, TObject self, PyObject instance, PyObject value);

        [PySpecialMethod("__delete__", typeof(PyBinaryFunction))]
        static partial void Delete(PyCallContext context, TObject self, PyObject instance);

        [PySpecialMethod("__set_name__", typeof(PyTernaryFunction))]
        static partial void SetName(PyCallContext context, TObject self, PyObject owner, PyObject name);

        [PySpecialMethod("__format__", typeof(PyBinaryFunction))]
        static partial void Format(PyCallContext context, TObject self, PyObject formatSpec);

        [PySpecialMethod("__iadd__", typeof(PyBinaryFunction))]
        static partial void IAdd(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__isub__", typeof(PyBinaryFunction))]
        static partial void ISub(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__imul__", typeof(PyBinaryFunction))]
        static partial void IMul(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__imatmul__", typeof(PyBinaryFunction))]
        static partial void IMatMul(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__itruediv__", typeof(PyBinaryFunction))]
        static partial void ITrueDiv(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__ifloordiv__", typeof(PyBinaryFunction))]
        static partial void IFloorDiv(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__imod__", typeof(PyBinaryFunction))]
        static partial void IMod(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__ipow__", typeof(PyTernaryFunction))]
        static partial void IPow(PyCallContext context, TObject self, PyObject other, PyObject modulo);

        [PySpecialMethod("__ilshift__", typeof(PyBinaryFunction))]
        static partial void ILShift(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__irshift__", typeof(PyBinaryFunction))]
        static partial void IRShift(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__iand__", typeof(PyBinaryFunction))]
        static partial void IAnd(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__ixor__", typeof(PyBinaryFunction))]
        static partial void IXor(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__ior__", typeof(PyBinaryFunction))]
        static partial void IOr(PyCallContext context, TObject self, PyObject other);

        [PySpecialMethod("__enter__", typeof(PyUnaryFunction))]
        static partial void Enter(PyCallContext context, TObject self);

        [PySpecialMethod("__exit__", typeof(PyQuaternaryFunction))]
        static partial void Exit(PyCallContext context, TObject self, PyObject excType, PyObject excVal, PyObject excTb);

        [PySpecialMethod("__await__", typeof(PyUnaryFunction))]
        static partial void Await(PyCallContext context, TObject self);

        [PySpecialMethod("__reversed__", typeof(PyUnaryFunction))]
        static partial void Reversed(PyCallContext context, TObject self);

#pragma warning restore IDE0060
#pragma warning restore IDE0051
    }
}

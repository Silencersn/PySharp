using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Typing;

/// <summary>
/// Type object for <c>typing.Generic</c>.
/// Provides <c>__class_getitem__</c> that creates a <c>GenericAlias</c>.
/// When a user-defined class inherits from Generic, the <c>__class_getitem__</c>
/// is found via MRO lookup and invoked when <c>SomeClass[int]</c> is written.
/// </summary>
[PyType("Generic", Module = "typing")]
internal sealed partial class PyGenericObjectType : PyTypeObject<PyGenericObject>
{
    /// <summary>
    /// <c>__class_getitem__</c> classmethod.
    /// Creates a <c>GenericAlias(cls, args)</c> when subscripting a generic class.
    /// Matches CPython's <c>Generic.__class_getitem__</c>.
    /// </summary>
    [PyClassMethod(PySpecialNames.ClassGetItem)]
    [PyFunctionParameters("cls", "*args")]
    private static PyResult ClassGetItem(PyCallContext context, PyTypeObject cls, PyArguments arguments)
    {
        // arguments are the user-supplied args (not including cls)
        // For Box[int], cls=Box, arguments=(int,)
        if (arguments.Args.Length is 0)
            return PyResult.TypeError($"{PySpecialNames.ClassGetItem} requires at least 1 argument");

        PyTupleObject argsTuple;

        if (arguments.Args.Length is 1)
            argsTuple = arguments[0] is PyTupleObject t ? t : PyTupleObject.CreateTuple(arguments[0]);
        else
            argsTuple = PyTupleObject.CreateTuple(arguments.Args);

        return new PyGenericAliasObject(cls, argsTuple);
    }

    /// <summary>
    /// <c>__init_subclass__</c> classmethod.
    /// In CPython, this collects type parameters from <c>__orig_bases__</c>
    /// and sets <c>__parameters__</c>.
    /// NOTE: Simplified for the initial generic class implementation.
    /// Full implementation requires TypeVar runtime objects, which are out of scope
    /// until class-body type-parameter references (e.g. <c>def method(self, x: T)</c>) are needed.
    /// Without this, <c>cls.__parameters__</c> stays empty, and subscript arg-count
    /// validation is skipped — acceptable until TypeVar support is added.
    /// </summary>
    [PyClassMethod(PySpecialNames.InitSubclass)]
    [PyFunctionParameters("cls", "*args")]
    private static PyResult InitSubclass(PyCallContext context, PyTypeObject cls, PyArguments arguments)
    {
        // Minimal: no TypeVar objects exist yet, so __parameters__ is not set.
        return PyNoneObject.None;
    }
}

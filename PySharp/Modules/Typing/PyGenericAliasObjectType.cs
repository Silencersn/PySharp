using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Typing;

/// <summary>
/// Type object for <see cref="PyGenericAliasObject"/>.
/// Corresponds to CPython's <c>types.GenericAliasType</c>.
/// Provides <c>__origin__</c>, <c>__args__</c> properties and delegates <c>__call__</c> to the origin.
/// </summary>
[PyType("GenericAlias", Module = "types")]
[AIGenerated]
internal sealed partial class PyGenericAliasObjectType : PyTypeObject<PyGenericAliasObject>
{
    protected override PyResult Repr(PyCallContext context, PyGenericAliasObject self)
    {
        // Format each type arg similar to CPython's _Py_typing_type_repr:
        // - PyTypeObject → use its Name (e.g. "int", "str")
        // - other objects → use repr() (e.g. "'hello'", "True")
        var argsReprs = new List<string>(self._args.Count);
        foreach (var arg in self._args)
        {
            if (arg is PyTypeObject argType)
            {
                argsReprs.Add(argType.Name);
            }
            else
            {
                var argRepr = PySpecialMethods.Repr(context, arg);
                if (argRepr.IsError)
                    return argRepr;
                argsReprs.Add(argRepr.Value is PyStrObject s ? s.Value : "?");
            }
        }

        // Origin name: use FullName (omits "builtins." prefix) for types, repr for others
        var originName = self._origin switch
        {
            PyTypeObject t => t.FullName,
            _ => PySpecialMethods.Repr(context, self._origin).Value is PyStrObject s ? s.Value : "?"
        };

        return PyStrObject.FromString($"{originName}[{string.Join(", ", argsReprs)}]");
    }

    /// <summary>
    /// <c>__origin__</c> property — the original unparameterized type.
    /// </summary>
    [PyProperty(PySpecialNames.Origin)]
    private static PyResult Get_Origin(PyCallContext context, PyGenericAliasObject self)
    {
        return self._origin;
    }

    /// <summary>
    /// <c>__args__</c> property — the type arguments tuple.
    /// </summary>
    [PyProperty(PySpecialNames.Args)]
    private static PyResult Get_Args(PyCallContext context, PyGenericAliasObject self)
    {
        return self._args;
    }

    /// <summary>
    /// <c>__parameters__</c> property — the type parameters if any.
    /// For now, returns empty tuple since we don't have TypeVar objects.
    /// </summary>
    [PyProperty(PySpecialNames.Parameters)]
    private static PyResult Get_Parameters(PyCallContext context, PyGenericAliasObject self)
    {
        return PyTupleObject.Empty;
    }

    /// <summary>
    /// <c>__call__</c> — delegates to the origin type's constructor.
    /// This enables <c>Box[int]()</c> syntax.
    /// </summary>
    protected override PyResult Call(PyCallContext context, PyGenericAliasObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        // Delegate to the underlying type's __call__ (which handles __new__ + __init__)
        if (self._origin is PyTypeObject typeObj)
            return typeObj.Call(context, args, kwargs);

        // Fallback: try calling the origin as a callable
        var callFunc = self._origin.PyType.Slots.Call;
        if (callFunc is not null)
            return callFunc(context, self._origin, args, kwargs);

        return PyResult.TypeError(PySR.Format(PySR.Runtime_Type_CannotCreateInstance, self._origin.PyType.FullName));
    }

    /// <summary>
    /// <c>__getattr__</c> — proxies unknown attribute access to the origin.
    /// This allows <c>Box[int].__name__</c> → <c>Box.__name__</c>.
    /// </summary>
    protected override PyResult GetAttr(PyCallContext context, PyGenericAliasObject self, PyObject item)
    {
        if (item is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.FullName);

        // First check our own attributes
        if (self.PyAttributes.TryGetValue(str.Value, out var ownAttr))
            return ownAttr;

        // Then proxy to origin's attribute
        var getAttrFunc = self._origin.PyType.Slots.GetAttr;
        if (getAttrFunc is not null)
        {
            var result = getAttrFunc(context, self._origin, item);
            if (!result.IsError)
                return result;
        }

        // Fallback: try GetAttribute
        var getAttributeFunc = self._origin.PyType.Slots.GetAttribute;
        if (getAttributeFunc is not null)
            return getAttributeFunc(context, self._origin, item);

        return PyResult.AttributeError($"'{self.PyType.FullName}' object has no attribute '{str.Value}'");
    }
}

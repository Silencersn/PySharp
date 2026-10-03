using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

// types.SimpleNamespace: a plain attribute bag whose keyword arguments seed
// the instance dict; sys.implementation is built on it (CPython 3.14 ships
// its implementation info as a SimpleNamespace)
public sealed class PySimpleNamespaceObject : PyObjectManagedDict
{
    internal PySimpleNamespaceObject()
    {
    }

    public override PyTypeObject DefaultPyType => PySimpleNamespaceObjectType.Shared;
}

// types.SimpleNamespace: a plain attribute bag whose keyword arguments seed
// the instance dict; sys.implementation is built on it (CPython 3.14 ships
// its implementation info as a SimpleNamespace). The Python-visible name
// lives in the types module — the Module metadata keeps the repr face at
// "types.SimpleNamespace" even before a types module exposes the name
[PyType("SimpleNamespace", Module = "types")]
public sealed partial class PySimpleNamespaceObjectType : PyTypeObject<PySimpleNamespaceObject>
{
    // CPython namespace_new only allocates; the argument consumption lives
    // in namespace_init so a subclass overriding __init__ replaces it
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return new PySimpleNamespaceObject { _pyType = cls };
    }

    // CPython namespace_init: at most one positional — an exact dict or
    // anything dict() accepts — updated into the bag, then the keywords
    protected override PyResult Init(PyCallContext context, PySimpleNamespaceObject self, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count > 1)
            return PyResult.TypeError(PySR.Runtime_SimpleNamespace_TooManyArguments, args.Count);

        if (args.Count is 1)
        {
            var arg = args[0];
            if (arg is PyDictObject exactDict)
            {
                var fill = FillFromDict(self, exactDict);
                if (fill.IsError)
                    return fill;
            }
            else
            {
                var converted = PyUtils.IterableToDict(context, arg);
                if (converted.IsError)
                    return converted;
                var fill = FillFromDict(self, converted.Value);
                if (fill.IsError)
                    return fill;
            }
        }

        foreach (var pair in kwargs)
            self.PyAttributes[pair.Key] = pair.Value;
        return PyNoneObject.None;
    }

    private static PyResult FillFromDict(PySimpleNamespaceObject ns, PyDictObject dict)
    {
        // PyArg_ValidateKeywordArguments: every key must be a string
        foreach (var pair in dict)
        {
            if (pair.Key is not PyStrObject key)
                return PyResult.TypeError(PySR.Runtime_Keyword_KeywordsMustBeStrings);
            ns.PyAttributes[key.Value] = pair.Value;
        }
        return PyNoneObject.None;
    }

    // CPython namespace_repr names the exact type "namespace" and any
    // subclass by its own type name
    protected override PyResult Repr(PyCallContext context, PySimpleNamespaceObject self)
    {
        var typeName = ReferenceEquals(self.PyType, PySimpleNamespaceObjectType.Shared)
            ? "namespace"
            : self.PyType.Name;
        var builder = new System.Text.StringBuilder(typeName).Append('(');
        var first = true;
        foreach (var pair in self.PyAttributes)
        {
            if (!first)
                builder.Append(", ");
            first = false;

            var valueRepr = PySpecialMethods.Repr(context, pair.Value);
            if (valueRepr.IsError)
                return valueRepr;
            builder.Append(pair.Key).Append('=').Append(valueRepr.Value.Value);
        }
        builder.Append(')');
        return PyStrObject.FromString(builder.ToString());
    }
}

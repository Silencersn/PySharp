using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

// types.SimpleNamespace: a plain attribute bag whose keyword arguments seed
// the instance dict; sys.implementation is built on it (CPython 3.14 ships
// its implementation info as a SimpleNamespace)
public sealed class PySimpleNamespaceObject : PyObjectManagedDict
{
    public override PyTypeObject DefaultPyType => PySimpleNamespaceObjectType.Shared;
}

[PyType("SimpleNamespace")]
public sealed partial class PySimpleNamespaceObjectType : PyTypeObject<PySimpleNamespaceObject>
{
    // CPython namespace_init: at most one positional — an exact dict or
    // anything dict() accepts — updated into the bag, then the keywords
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (args.Count > 1)
            return PyResult.TypeError(PySR.Runtime_SimpleNamespace_TooManyArguments, args.Count);

        var ns = new PySimpleNamespaceObject { _pyType = cls };
        if (args.Count is 1)
        {
            var arg = args[0];
            if (arg is PyDictObject exactDict)
            {
                var fill = FillFromDict(context, ns, exactDict);
                if (fill.IsError)
                    return fill;
            }
            else
            {
                var converted = PyUtils.IterableToDict(context, arg);
                if (converted.IsError)
                    return converted;
                var fill = FillFromDict(context, ns, converted.Value);
                if (fill.IsError)
                    return fill;
            }
        }

        foreach (var pair in kwargs)
            ns.PyAttributes[pair.Key] = pair.Value;
        return ns;
    }

    private static PyResult FillFromDict(PyCallContext context, PySimpleNamespaceObject ns, PyDictObject dict)
    {
        // PyArg_ValidateKeywordArguments: every key must be a string
        foreach (var pair in dict)
        {
            if (pair.Key is not PyStrObject key)
                return PyResult.TypeError(PySR.Runtime_SimpleNamespace_KeyMustBeStr, pair.Key.PyType.Name);
            ns.PyAttributes[key.Value] = pair.Value;
        }
        return PyNoneObject.None;
    }

    // CPython SimpleNamespace_repr: "namespace(a=1, b=2)" in insertion order
    protected override PyResult Repr(PyCallContext context, PySimpleNamespaceObject self)
    {
        var builder = new System.Text.StringBuilder("namespace(");
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

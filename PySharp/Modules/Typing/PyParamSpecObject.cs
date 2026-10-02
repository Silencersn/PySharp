using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Typing;

/// <summary>
/// Represents a parameter specification created from the PEP 695 generic
/// syntax (e.g. <c>class C[**P]:</c>) or <c>typing.ParamSpec</c>.
/// Corresponds to CPython's <c>typing.ParamSpec</c> (typevarobject.c).
/// </summary>
internal sealed class PyParamSpecObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyParamSpecObjectType.Shared;

    internal readonly string _name;

    // PEP 695 type params are created with infer_variance=true (the
    // intrinsic path); a manual typing.ParamSpec call infers nothing, so
    // CPython prefixes the repr with the invariant marker '~'
    internal readonly bool _inferVariance;

    internal PyParamSpecObject(string name, bool inferVariance)
    {
        _name = name;
        _inferVariance = inferVariance;
    }
}

[PyType("ParamSpec", Module = "typing")]
[AIGenerated]
internal sealed partial class PyParamSpecObjectType : PyTypeObject<PyParamSpecObject>
{
    // CPython paramspec_repr: the name when variance is inferred, else the
    // invariant marker prefix; variance keywords are not supported yet so
    // only these two shapes occur
    protected override PyResult Repr(PyCallContext context, PyParamSpecObject self)
    {
        return PyStrObject.FromString(self._inferVariance ? self._name : "~" + self._name);
    }

    // CPython paramspec_new: only the name is required; the bound, default,
    // and variance keywords are not yet supported here
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError("ParamSpec() takes no keyword arguments");

        if (args.Count is not 1 || args[0] is not PyStrObject name)
            return PyResult.TypeError("ParamSpec() argument 'name' must be str");

        return new PyParamSpecObject(name.Value, inferVariance: false);
    }

    [PyProperty(PySpecialNames.Name)]
    private static PyResult Get_Name(PyCallContext context, PyParamSpecObject self)
    {
        return PyStrObject.FromString(self._name);
    }

    [PyProperty("__bound__")]
    private static PyResult Get_Bound(PyCallContext context, PyParamSpecObject self)
    {
        return PyNoneObject.None;
    }

    [PyProperty("__default__")]
    private static PyResult Get_Default(PyCallContext context, PyParamSpecObject self)
    {
        return PyNoneObject.None;
    }

    [PyProperty("__covariant__")]
    private static PyResult Get_Covariant(PyCallContext context, PyParamSpecObject self)
    {
        return PyBoolObject.False;
    }

    [PyProperty("__contravariant__")]
    private static PyResult Get_Contravariant(PyCallContext context, PyParamSpecObject self)
    {
        return PyBoolObject.False;
    }

    [PyProperty("__infer_variance__")]
    private static PyResult Get_InferVariance(PyCallContext context, PyParamSpecObject self)
    {
        return PyBoolObject.FromBoolean(self._inferVariance);
    }

    // CPython paramspec_args/paramspec_kwargs: fresh ParamSpecArgs/Kwargs
    // instances carrying the ParamSpec back as __origin__
    [PyProperty("args")]
    private static PyResult Get_Args(PyCallContext context, PyParamSpecObject self)
    {
        return new PyParamSpecArgsObject(self);
    }

    [PyProperty("kwargs")]
    private static PyResult Get_Kwargs(PyCallContext context, PyParamSpecObject self)
    {
        return new PyParamSpecKwargsObject(self);
    }
}

/// <summary>
/// Represents <c>P.args</c> for a <see cref="PyParamSpecObject"/>.
/// Corresponds to CPython's <c>typing.ParamSpecArgs</c>.
/// </summary>
internal sealed class PyParamSpecArgsObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyParamSpecArgsObjectType.Shared;

    internal readonly PyObject _origin;

    internal PyParamSpecArgsObject(PyObject origin)
    {
        _origin = origin;
    }
}

[PyType("ParamSpecArgs", Module = "typing")]
[AIGenerated]
internal sealed partial class PyParamSpecArgsObjectType : PyTypeObject<PyParamSpecArgsObject>
{
    // CPython paramspecargs_repr: "<name>.args" for a ParamSpec origin,
    // the repr of anything else
    protected override PyResult Repr(PyCallContext context, PyParamSpecArgsObject self)
    {
        var suffix = self._origin is PyParamSpecObject spec ? $"{spec._name}.args" : $"{PySpecialMethods.Repr(context, self._origin).Value}.args";
        return PyStrObject.FromString(suffix);
    }

    // CPython paramspecargs_new: requires the origin object
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError("ParamSpecArgs() takes no keyword arguments");

        if (args.Count is not 1)
            return PyResult.TypeError("ParamSpecArgs() takes exactly one argument");

        return new PyParamSpecArgsObject(args[0]);
    }

    [PyProperty(PySpecialNames.Origin)]
    private static PyResult Get_Origin(PyCallContext context, PyParamSpecArgsObject self)
    {
        return self._origin;
    }
}

/// <summary>
/// Represents <c>P.kwargs</c> for a <see cref="PyParamSpecObject"/>.
/// Corresponds to CPython's <c>typing.ParamSpecKwargs</c>.
/// </summary>
internal sealed class PyParamSpecKwargsObject : PyObject
{
    public override PyTypeObject DefaultPyType => PyParamSpecKwargsObjectType.Shared;

    internal readonly PyObject _origin;

    internal PyParamSpecKwargsObject(PyObject origin)
    {
        _origin = origin;
    }
}

[PyType("ParamSpecKwargs", Module = "typing")]
[AIGenerated]
internal sealed partial class PyParamSpecKwargsObjectType : PyTypeObject<PyParamSpecKwargsObject>
{
    // CPython paramspeckwargs_repr: "<name>.kwargs" for a ParamSpec origin,
    // the repr of anything else
    protected override PyResult Repr(PyCallContext context, PyParamSpecKwargsObject self)
    {
        var suffix = self._origin is PyParamSpecObject spec ? $"{spec._name}.kwargs" : $"{PySpecialMethods.Repr(context, self._origin).Value}.kwargs";
        return PyStrObject.FromString(suffix);
    }

    // CPython paramspeckwargs_new: requires the origin object
    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (kwargs.Count is not 0)
            return PyResult.TypeError("ParamSpecKwargs() takes no keyword arguments");

        if (args.Count is not 1)
            return PyResult.TypeError("ParamSpecKwargs() takes exactly one argument");

        return new PyParamSpecKwargsObject(args[0]);
    }

    [PyProperty(PySpecialNames.Origin)]
    private static PyResult Get_Origin(PyCallContext context, PyParamSpecKwargsObject self)
    {
        return self._origin;
    }
}

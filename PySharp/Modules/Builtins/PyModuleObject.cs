using PySharp.Compilation;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;
using System.ComponentModel;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

public class PyModuleObject : PyObjectManagedDict, IPyObjectName
{
    public string Name { get; }

    // CPython's module repr derives its source annotation from the module
    // spec (origin "built-in"/"frozen" parenthesized, a location origin as
    // "from 'path'"). Without a spec system the module records its kind at
    // creation: "built-in" for C#-implemented stdlib modules, "frozen" for
    // embedded-source modules, "namespace" for packages without __init__.py.
    public string? Origin { get; internal set; }

    public override PyTypeObject DefaultPyType => PyModuleObjectType.Shared;
    internal PyDictObject PyAttributesDict => (PyDictObject)_pyAttributes!;

    public PyModuleObject(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;

        _pyAttributes = new PyDictObject();

        PyAttributes[PySpecialNames.Name] = PyStrObject.FromString(Name);
        // Default __package__: parent package name (empty for top-level modules)
        var lastDot = name.LastIndexOf('.');
        PyAttributes[PySpecialNames.Package] = lastDot >= 0 ? PyStrObject.FromString(name[..lastDot]) : PyStrObject.Empty;
        ApplyIncludes();

        Debug.Assert(_pyAttributes is not null);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected internal void AppendAttribute<TPyObject>(TPyObject pyObject) where TPyObject : PyObject, IPyObjectName
    {
        PyAttributes[pyObject.Name] = pyObject;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected internal void AppendAttribute(string name, PyObject pyObject)
    {
        ArgumentNullException.ThrowIfNull(pyObject);
        PyAttributes[name] = pyObject;
    }

    public virtual void OnImport(PyCallContext context, PyEnvironment environment) { }

    /// <summary>
    /// Called during construction to register module attributes.
    /// Overridden by source-generated code when <see cref="PyModuleIncludeAttribute"/> is used.
    /// </summary>
    protected virtual void ApplyIncludes() { }

    internal static PyModuleObject CreatePackage(string name, IReadOnlyList<string> paths)
    {
        var package = new PyModuleObject(name);
        package.PyAttributes[PySpecialNames.Path] = PyListObject.CreateList(paths.Select(PyStrObject.FromString));
        // For packages, __package__ should be the same as __name__
        package.PyAttributes[PySpecialNames.Package] = PyStrObject.FromString(name);
        return package;
    }
}

[PyType("module")]
public sealed partial class PyModuleObjectType : PyTypeObject<PyModuleObject>
{

    protected override PyResult Repr(PyCallContext context, PyModuleObject self)
    {
        // CPython _module_repr precedence: the spec's origin annotation wins
        // over any __file__, a namespace package lists its __path__, and a
        // file location renders as "from 'path'"; everything else is bare.
        switch (self.Origin)
        {
            case "built-in" or "frozen":
                return PyStrObject.FromString($"<module '{self.Name}' ({self.Origin})>");

            case "namespace":
                if (self.PyAttributes.TryGetValue(PySpecialNames.Path, out var pathValue)
                    && PyUtils.IterableToList(context, pathValue) is { IsError: false } paths)
                {
                    var pathRepr = PySpecialMethods.Repr(context, paths.Value);
                    if (pathRepr.IsSuccessful)
                        return PyStrObject.FromString($"<module '{self.Name}' (namespace) from {pathRepr.Value.Value}>");
                }

                return PyStrObject.FromString($"<module '{self.Name}' (namespace)>");
        }

        if (self.PyAttributes.TryGetValue(PySpecialNames.File, out var file) && file is PyStrObject fileStr)
            return PyStrObject.FromString($"<module '{self.Name}' from '{fileStr.Value}'>");

        return PyStrObject.FromString($"<module '{self.Name}'>");
    }

    [PyProperty(PySpecialNames.Dict)]
    private static PyResult Get_Dict(PyCallContext context, PyModuleObject self)
    {
        return (PyDictObject)self.PyAttributes;
    }

    [PyProperty(PySpecialNames.Annotations)]
    private static PyResult Get_Annotations(PyCallContext context, PyModuleObject self)
    {
        if (self.PyAttributes.TryGetValue(PySpecialNames.Annotations, out var existing))
            return existing;

        return self.PyAttributes[PySpecialNames.Annotations] = new PyDictObject();
    }

    [PyProperty(PySpecialNames.Annotations, Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Annotations(PyCallContext context, PyModuleObject self, PyObject value)
    {
        if (value is not PyDictObject && value is not PyNoneObject)
            return PyResult.TypeError("__annotations__ must be set to a dict object");

        self.PyAttributes[PySpecialNames.Annotations] = value;
        self.PyAttributes.Remove(PySpecialNames.Annotate);
        return PyNoneObject.None;
    }

    [PyProperty(PySpecialNames.Annotations, Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Annotations(PyCallContext context, PyModuleObject self)
    {
        if (!self.PyAttributes.Remove(PySpecialNames.Annotations))
            return PyResult.AttributeError(PySR.Runtime_Object_AttributeNotFound, PySpecialNames.Annotations);

        self.PyAttributes.Remove(PySpecialNames.Annotate);
        return PyNoneObject.None;
    }

    protected override PyResult GetAttr(PyCallContext context, PyModuleObject self, PyObject item)
    {
        if (item is not PyStrObject str)
            return PyResult.TypeError(PySR.Runtime_Object_AttributeMustBeString, item.PyType.QualName);

        // PEP 562: on a namespace miss, a module-level __getattr__ resolves
        // the attribute; its exceptions (AttributeError included) propagate
        // unchanged, exactly as CPython's module_getattro passes the hook
        // result through unchecked
        if (self.PyAttributes.TryGetValue(PySpecialNames.GetAttr, out var hook))
            return hook.Call(context, [str]);

        return PyResult.AttributeError(PySR.Runtime_Module_AttributeNotFound, self.Name, str.Value);
    }

    // CPython module.__dir__ (PEP 562): the module dict's __dir__ hook
    // produces the name list, otherwise the plain dict keys are listed;
    // dir() itself sorts the result
    [PyMethod(PySpecialNames.Dir)]
    [PyFunctionParameters()]
    private static PyResult Dir(PyCallContext context, PyModuleObject self, PyArguments arguments)
    {
        if (self.PyAttributes.TryGetValue(PySpecialNames.Dir, out var hook))
            return hook.Call(context);

        List<PyObject> keys = [];
        foreach (var pair in self.PyAttributes)
            keys.Add(PyStrObject.FromString(pair.Key));
        return PyListObject.CreateList(keys);
    }
}

public abstract class PyFrozenModuleObject : PyModuleObject
{
    protected PyFrozenModuleObject(string name) : base(name)
    {
        Origin = "frozen";
    }

    public abstract string Code { get; }

    private PyCodeObject? CodeObject;

    public override void OnImport(PyCallContext context, PyEnvironment environment)
    {
        CodeObject ??= Compiler.InternalCompileExec(context, Code, $"{Name}.py", Name);
        PyInterpreter.InternalExecuteToModule(context, CodeObject, this, isMain: false);
    }
}

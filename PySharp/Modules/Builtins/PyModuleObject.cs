using PySharp.Compilation;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;
using PySharp.Utility;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;

namespace PySharp.Modules.Builtins;

public class PyModuleObject : PyObjectManagedDict, IPyObjectName
{
    private readonly PyDictObject _dict;

    public string Name { get; }
    public virtual string? Origin => null;
    public override PyTypeObject DefaultPyType => PyModuleObjectType.Shared;

    internal sealed override IDictionary<string, PyObject> PyAttributes
    {
        get => _pyAttributes ??= new StringKeyDict(_dict);
        set => throw new NotSupportedException();
    }

    internal PyDictObject PyAttributesDict => _dict;

    public PyModuleObject(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;

        _dict = [];

        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        // Default __package__: parent package name (empty for top-level modules)
        var lastDot = name.LastIndexOf('.');
        PyAttributes.Add(PySpecialNames.Package, lastDot >= 0 ? PyStrObject.FromString(name[..lastDot]) : PyStrObject.Empty);
        ApplyIncludes();

        Debug.Assert(_dict is not null);
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
        package.PyAttributes.Add(PySpecialNames.Path, PyListObject.CreateList(paths.Select(PyStrObject.FromString)));
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
        if (self.Origin is not null)
            return PyStrObject.FromString($"<module '{self.Name}' ({self.Origin})>");
        return PyStrObject.FromString($"<module '{self.Name}'>");
    }

    [PyProperty(PySpecialNames.Dict)]
    private static PyResult Get_Dict(PyCallContext context, PyModuleObject self)
    {
        return PyDictObject.CreateProxy(new DictAdapter(self.PyAttributes!));
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
        return PyResult.AttributeError($"module '{self.Name}' has no attribute '{item}'");
    }
}

public abstract class PyFrozenModuleObject : PyModuleObject
{
    public sealed override string? Origin => "frozen";

    protected PyFrozenModuleObject(string name) : base(name)
    {
    }

    public abstract string Code { get; }

    private PyCodeObject? CodeObject;

    public override void OnImport(PyCallContext context, PyEnvironment environment)
    {
        CodeObject ??= Compiler.InternalCompileExec(context, Code, $"{Name}.py", Name);
        PyInterpreter.InternalExecuteToModule(context, CodeObject, this, isMain: false);
    }
}

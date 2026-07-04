using PySharp.Compilation.Bytecodes;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;
using PySharp.Utility;
using System.ComponentModel;

namespace PySharp.Modules.Builtins;

public class PyModuleObject : PyObjectManagedDict, IPyObjectName
{
    public string Name { get; }
    public virtual string? Origin => null;
    public override PyTypeObject DefaultPyType => PyModuleObjectType.Shared;

    public PyModuleObject(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
        ApplyIncludes();
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

    [PyProperty("__dict__")]
    private static PyResult Get_Dict(PyCallContext context, PyModuleObject self)
    {
        return PyDictObject.CreateProxy(new DictAdapter(self.PyAttributes!));
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
        CodeObject ??= Compiler.CompileExec(context, Code, $"{Name}.py", Name);
        PyInterpreter.InternalExecuteToModule(context, CodeObject, this, isMain: false);
    }
}

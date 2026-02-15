using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

public class PyModuleObject : PyObject, IPyObjectName
{
    public string Name { get; }
    public virtual string? ReprPrompt => null;
    public override PyTypeObject DefaultPyType => PyModuleObjectType.Shared;

    public PyModuleObject(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        PyAttributes.Add(PySpecialNames.Name, PyStrObject.FromString(Name));
    }

    internal void AddObjToAttrs<TPyObject>(TPyObject pyObject) where TPyObject : PyObject, IPyObjectName
    {
        PyAttributes[pyObject.Name] = pyObject;
    }

    internal void AddObjToAttrs<TPyObject>(string name, [NotNull] TPyObject? pyObject) where TPyObject : PyObject
    {
        Debug.Assert(pyObject is not null);
        PyAttributes[name] = pyObject;
    }

    public virtual void OnImport(PyCallContext context, PyEnvironment environment) { }
}

public sealed class PyModuleObjectType : PyTypeObject<PyModuleObjectType, PyModuleObject>
{
    public override string Module => "builtins";
    public override string Name => "module";

    protected override PyResult Repr(PyCallContext context, PyModuleObject self)
    {
        if (self.ReprPrompt is not null)
            return PyStrObject.FromString($"<module '{self.Name}' {self.ReprPrompt}>");
        return PyStrObject.FromString($"<module '{self.Name}'>");
    }

    protected override PyResult GetAttr(PyCallContext context, PyModuleObject self, PyObject item)
    {
        return PyResult.AttributeError($"module '{self.Name}' has no attribute '{item}'");
    }
}

public abstract class PyCodeBasedModuleObject : PyModuleObject
{
    protected PyCodeBasedModuleObject(string name) : base(name)
    {
    }

    public abstract string Code { get; }

    public override void OnImport(PyCallContext context, PyEnvironment environment)
    {
        PyInterpreter.RunCodeWithContext(context, Code, this, $"{Name}.py");
    }
}
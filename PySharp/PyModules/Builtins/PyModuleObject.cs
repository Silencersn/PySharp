using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Environments;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyModules.Builtins;

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

    public virtual void OnImport(PyEnvironment environment) { }
}

public sealed class PyModuleObjectType : PyTypeObject<PyModuleObjectType, PyModuleObject>
{
    public override string Name => "module";

    protected internal override PyResult Repr(PyCallContext context, PyModuleObject self)
    {
        if (self.ReprPrompt is not null)
            return PyStrObject.FromString($"<module '{self.Name}' {self.ReprPrompt}>");
        return PyStrObject.FromString($"<module '{self.Name}'>");
    }

    protected internal override PyResult GetAttr(PyCallContext context, PyModuleObject self, string item)
    {
        return PyResult.RaiseAttributeError($"module '{self.Name}' has no attribute '{item}'");
    }
}

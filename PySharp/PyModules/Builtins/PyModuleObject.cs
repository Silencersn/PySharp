using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.Environments;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyModules.Builtins;

public class PyModuleObject : PyObject, IPyObjectName
{
    public string Name { get; }

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

    protected internal override PyObject? ReprImpl()
    {
        return PyStrObject.FromString($"<module '{Name}'>");
    }

    protected internal override PyObject? GetAttrImpl(string item)
    {
        return PyVirtualMachine.RaiseAttributeError($"module '{Name}' has no attribute '{item}'");
    }

    public virtual void OnImport(PyEnvironment environment)
    {

    }
}

public sealed class PyModuleObjectType : PyPrimitiveTypeObject<PyModuleObjectType, PyModuleObject>
{
    public override string Name => "module";

    protected internal override PyObject? NewImpl(PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var pack = new PyArgsPack(args, kwargs);
        if (pack.TryParseOneArgOrOneKwarg("name", out var arg))
        {
            if (arg is not PyStrObject str)
                return PyVirtualMachine.RaiseTypeError($"module() argument 'name' must be str, not {arg.PyType.Name}");

            return new PyModuleObject(str.Value);
        }

        if (pack.Count is 0)
            return PyVirtualMachine.RaiseTypeError("module() missing required argument 'name' (pos 1)");

        throw new NotImplementedException();
    }
}

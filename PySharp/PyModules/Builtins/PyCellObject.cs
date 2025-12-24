using PySharp.PyRuntime.Calls;

namespace PySharp.PyModules.Builtins;

public class PyCellObject : PyObject
{
    internal string Name { get; }
    public PyObject? Value { get; set; }

    public override PyTypeObject DefaultPyType => PyCellObjectType.Shared;

    private PyCellObject(string name, PyObject? value)
    {
        Name = name;
        Value = value;
    }

    public static PyCellObject CreateCell(string name, PyObject? value)
    {
        return new PyCellObject(name, value);
    }
}

public sealed class PyCellObjectType : PyTypeObject<PyCellObjectType, PyCellObject>
{
    public override string Name => "cell";

    protected internal override PyResult Repr(PyCallContext context, PyCellObject self)
    {
        if (self.Value is null)
            return PyStrObject.FromString($"<cell at 0x{self.PyId:X16}: empty>");
        return PyStrObject.FromString($"<cell at 0x{self.PyId:X16}: {self.Value.PyType.Name} object at 0x{self.Value.PyId:X16}>");
    }
}
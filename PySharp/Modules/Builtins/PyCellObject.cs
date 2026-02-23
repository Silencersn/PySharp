using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

public class PyCellObject : PyObject
{
    public PyObject? Value { get; set; }

    public override PyTypeObject DefaultPyType => PyCellObjectType.Shared;

    private PyCellObject(PyObject? value)
    {
        Value = value;
    }

    public static PyCellObject CreateEmpty()
    {
        return new PyCellObject(value: null);
    }

    public static PyCellObject CreateCell(PyObject? value)
    {
        return new PyCellObject(value);
    }
}

[PyType("cell")]
public sealed partial class PyCellObjectType : PyTypeObject<PyCellObjectType, PyCellObject>
{

    protected override PyResult Repr(PyCallContext context, PyCellObject self)
    {
        if (self.Value is null)
            return PyStrObject.FromString($"<cell at 0x{self.PyId:X16}: empty>");
        return PyStrObject.FromString($"<cell at 0x{self.PyId:X16}: {self.Value.PyType.Name} object at 0x{self.Value.PyId:X16}>");
    }
}
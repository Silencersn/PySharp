namespace PySharp.PyModules.Builtins;

public class PyCellObject : PyObject
{
    internal string Name { get; }
    public PyObject? Value { get; set; }

    private PyCellObject(string name, PyObject? value)
    {
        Name = name;
        Value = value;
    }
    public override PyObject? Repr()
    {
        if (Value is null)
            return PyStrObject.FromString($"<cell at 0x{PyId:X16}: empty>");
        return PyStrObject.FromString($"<cell at 0x{PyId:X16}: {Value.PyType.Name} object at 0x{Value.PyId:X16}>");
    }

    public static PyCellObject CreateCell(string name, PyObject? value)
    {
        return new PyCellObject(name, value);
    }
}
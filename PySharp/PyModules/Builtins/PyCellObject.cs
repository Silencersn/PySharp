namespace PySharp.PyModules.Builtins;

public class PyCellObject : PyObject
{
    public PyObject? Value { get; set; }

    private PyCellObject(PyObject? value)
    {
        Value = value;
    }

    public static PyCellObject CreateCell(PyObject? value)
    {
        return new PyCellObject(value);
    }
}
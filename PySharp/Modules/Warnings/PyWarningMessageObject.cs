using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Warnings;

public sealed class PyWarningMessageObject : PyObject
{
    internal PyObject Message { get; }
    internal PyTypeObject<PyExceptionObject> Category { get; }
    internal PyStrObject Filename { get; }
    internal PyIntObject Lineno { get; }
    internal PyObject File { get; }
    internal PyObject Line { get; }

    internal PyWarningMessageObject(
        PyObject message,
        PyTypeObject<PyExceptionObject> category,
        string filename,
        int lineno,
        string? line)
    {
        Message = message;
        Category = category;
        Filename = PyStrObject.FromString(filename);
        Lineno = PyIntObject.FromInteger(lineno);
        File = PyNoneObject.None;
        Line = line is null ? PyNoneObject.None : PyStrObject.FromString(line);
    }

    public override PyTypeObject DefaultPyType => PyWarningMessageObjectType.Shared;
}

[PyType("warnings.WarningMessage")]
public sealed partial class PyWarningMessageObjectType : PyTypeObject<PyWarningMessageObject>
{
    [PyProperty("message")]
    private static PyResult GetMessage(PyCallContext context, PyWarningMessageObject self) => self.Message;

    [PyProperty("category")]
    private static PyResult GetCategory(PyCallContext context, PyWarningMessageObject self) => self.Category;

    [PyProperty("filename")]
    private static PyResult GetFilename(PyCallContext context, PyWarningMessageObject self) => self.Filename;

    [PyProperty("lineno")]
    private static PyResult GetLineno(PyCallContext context, PyWarningMessageObject self) => self.Lineno;

    [PyProperty("file")]
    private static PyResult GetFile(PyCallContext context, PyWarningMessageObject self) => self.File;

    [PyProperty("line")]
    private static PyResult GetLine(PyCallContext context, PyWarningMessageObject self) => self.Line;
}

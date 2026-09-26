using PySharp.Compilation.CodeAnalysis;
using PySharp.Resources;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Builtins;

// CPython's traceback object (Python/traceback.c): one node per frame the
// exception left, linked through tb_next towards the frame where the exception
// occurred. The exception's __traceback__ is therefore the outermost frame and
// the raise site is the tail — "Traceback (most recent call last)".
public sealed class PyTracebackObject : PyObject
{
    internal readonly CodeMetaInfo? _info;
    internal readonly string? _callerName;
    internal PyTracebackObject? _next;

    public override PyTypeObject DefaultPyType => PyTracebackObjectType.Shared;

    internal PyTracebackObject(CodeMetaInfo? info, string? callerName, PyTracebackObject? next = null)
    {
        _info = info;
        _callerName = callerName;
        _next = next;
    }
}

[PyType("traceback")]
internal sealed partial class PyTracebackObjectType : PyTypeObject<PyTracebackObject>
{
    // CPython exposes tb_lineno as a getset without a setter, so assignment
    // reports the generic descriptor message rather than "readonly attribute"
    // (Objects/descrobject.c getset_set).
    [PyProperty("tb_lineno")]
    private static PyResult Get_Lineno(PyCallContext context, PyTracebackObject self)
    {
        if (self._info is null)
            return PyNoneObject.None;

        return PyIntObject.FromInteger(self._info.Start.Line);
    }

    [PyProperty("tb_lineno", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Lineno(PyCallContext context, PyTracebackObject self, PyObject value)
    {
        return PyResult.AttributeError(PySR.Runtime_Traceback_LinenoNotWritable);
    }

    // CPython has no deleter either, and deleting a read-only getset reports
    // the same not-writable message as assignment
    [PyProperty("tb_lineno", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Lineno(PyCallContext context, PyTracebackObject self)
    {
        return PyResult.AttributeError(PySR.Runtime_Traceback_LinenoNotWritable);
    }

    [PyProperty("tb_next")]
    private static PyResult Get_Next(PyCallContext context, PyTracebackObject self)
    {
        return (PyObject?)self._next ?? PyNoneObject.None;
    }

    // CPython traceback_tb_next_set_impl accepts None or a traceback and
    // rejects a chain that leads back to self with ValueError.
    [PyProperty("tb_next", Type = PyPropertyMethodType.Setter)]
    private static PyResult Set_Next(PyCallContext context, PyTracebackObject self, PyObject value)
    {
        if (value is PyNoneObject)
        {
            self._next = null;
            return PyNoneObject.None;
        }

        if (value is not PyTracebackObject next)
            return PyResult.TypeError(PySR.Runtime_Traceback_NextExpectedTraceback, value.PyType.TpName);

        for (PyTracebackObject? cursor = next; cursor is not null; cursor = cursor._next)
        {
            if (ReferenceEquals(cursor, self))
                return PyResult.ValueError(PySR.Runtime_Traceback_LoopDetected);
        }

        self._next = next;
        return PyNoneObject.None;
    }

    [PyProperty("tb_next", Type = PyPropertyMethodType.Deleter)]
    private static PyResult Delete_Next(PyCallContext context, PyTracebackObject self)
    {
        return PyResult.TypeError(PySR.Runtime_Traceback_NextMayNotBeDeleted);
    }
}

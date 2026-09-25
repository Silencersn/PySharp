using PySharp.Modules.Builtins;
using PySharp.Modules.IO;
using PySharp.Runtime.Calls;
using System.Text;

namespace PySharp.Modules.Sys;

/// <summary>
/// Python wrapper for one of the standard streams (stdin/stdout/stderr).
/// Holds either a TextReader (stdin) or a TextWriter (stdout/stderr).
/// </summary>
public sealed class PyStdIoObject : PyObject
{
    private readonly TextReader? _reader;
    private readonly TextWriter? _writer;
    internal readonly string _name;
    private bool _closed;

    private PyStdIoObject(TextReader? reader, TextWriter? writer, string name)
    {
        _reader = reader;
        _writer = writer;
        _name = name;
    }

    public override PyTypeObject DefaultPyType => PyStdIoObjectType.Shared;

    internal static PyStdIoObject CreateInput(TextReader reader, string name) => new(reader, null, name);
    internal static PyStdIoObject CreateOutput(TextWriter writer, string name) => new(null, writer, name);

    internal bool IsReadable => _reader is not null;
    internal bool IsWritable => _writer is not null;
    internal bool IsClosed => _closed;

    internal PyResult CheckClosed()
    {
        if (_closed)
            return PyResult.ValueError(PySR.Runtime_File_Closed);
        return default;
    }

    // the flag queries use CPython's no-period closed message
    internal PyResult CheckClosedNoPeriod()
    {
        if (_closed)
            return PyResult.ValueError(PySR.Runtime_File_ClosedNoPeriod);
        return default;
    }

    // sys.stdin/stdout/stderr accept close() like CPython's TextIOWrapper
    // std streams: the closed flag is what matters (later operations raise
    // the closed-file ValueError through CheckClosed); the process-wide
    // console reader/writer itself stays usable for the host
    internal PyResult Close()
    {
        if (_closed)
            return PyNoneObject.None;
        _closed = true;
        try
        {
            _writer?.Flush();
        }
        catch (IOException)
        {
            // a failing final flush must not mask the successful close
        }
        return PyNoneObject.None;
    }

    internal PyResult Read(PyCallContext context, int size = -1)
    {
        var check = CheckClosed();
        if (check.IsError)
            return check;
        if (_reader is null)
            return PyResult.ValueError(PySR.Runtime_File_NotReadable);

        string result;
        if (size < 0)
        {
            result = _reader.ReadToEnd();
        }
        else if (size is 0)
        {
            result = string.Empty;
        }
        else
        {
            var buf = new char[size];
            var count = _reader.Read(buf, 0, size);
            result = new string(buf, 0, count);
        }
        return PyStrObject.FromString(result);
    }

    internal PyResult ReadLine(int size = -1)
    {
        var check = CheckClosed();
        if (check.IsError)
            return check;
        if (_reader is null)
            return PyResult.ValueError(PySR.Runtime_File_NotReadable);

        // Build the line character by character to preserve the trailing newline.
        var lineBuilder = new StringBuilder();
        int charsRead = 0;
        int ch;
        while ((ch = _reader.Read()) >= 0 && (size < 0 || charsRead < size))
        {
            lineBuilder.Append((char)ch);
            charsRead++;
            if (ch is '\n')
                break;
        }
        // readline() returns '' at EOF, unlike __next__ which raises StopIteration.
        return charsRead is 0
            ? (PyResult)PyStrObject.Empty
            : (PyResult)PyStrObject.FromString(lineBuilder.ToString());
    }

    internal PyResult Write(PyCallContext context, PyObject data)
    {
        var check = CheckClosed();
        if (check.IsError)
            return check;
        if (_writer is null)
            return PyResult.ValueError(PySR.Runtime_File_NotWritable);
        if (data is not PyStrObject strObj)
            return PyResult.TypeError(PySR.Runtime_File_WriteNeedStr, data.PyType.TpName);

        _writer.Write(strObj.Value);
        _writer.Flush();
        return PyIntObject.FromInteger(PyFileObject.CodePointLength(strObj.Value));
    }

    internal PyResult Flush()
    {
        var check = CheckClosed();
        if (check.IsError)
            return check;
        _writer?.Flush();
        return PyNoneObject.None;
    }
}

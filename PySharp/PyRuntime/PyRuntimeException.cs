using PySharp.PyModules.Builtins;

namespace PySharp.PyRuntime;

public class PyRuntimeException : Exception
{
    private readonly PyExceptionObject _exception;

    public PyRuntimeException(PyExceptionObject exception) : base(exception.ToMessage())
    {
        ArgumentNullException.ThrowIfNull(exception);

        _exception = exception;
        _exception.Traceback = PrintTraceback();
    }

    public PyExceptionObject PyException => _exception;

    public static string PrintTraceback()
    {
        Stack<string> stack = [];
        var frame = PyVirtualMachine.CurrentFrame;
        while (frame is not null)
        {
            var info = frame.Info;
            if (info is not null)
            {
                var metaInfo = info.MetaInfo;
                if (info.CurrentLine is not null)
                    stack.Push($"    {info.CurrentLine.Trim().TrimEnd('\r', '\n')}");
                stack.Push($"  File \"{metaInfo.SourceName ?? "<unknown>"}\", line {info.Lineno}, in {info.Name}");
            }

            frame = frame.Back;
        }
        return string.Join(Environment.NewLine, stack);
    }
}

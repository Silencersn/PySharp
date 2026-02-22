using PySharp.Modules.Builtins;

namespace PySharp.Runtime.Calls;

partial class PyCallContext
{
    public bool TryWarn(PyExceptionType warningType, string message)
    {
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        PyEnvironment.Error.WriteLine(warningType.Create(PyStrObject.FromString(message)).ToMessage(this));
        Console.ForegroundColor = color;
        return true;
    }

    public bool TryWarn<TWarning>(string message) where TWarning : PyExceptionType<TWarning>, IPyException<TWarning>, new()
    {
        return TryWarn(TWarning.Shared, message);
    }
}

using PySharp.PyModules.Builtins;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime;

partial class PyVirtualMachine
{
    [MemberNotNullWhen(false, nameof(CurrentException))]
    public static bool TryWarn(PyExceptionType warningType, string message)
    {
        PyEnvironment.Error.WriteLine(warningType.Create(PyStrObject.FromString(message)).ToMessage());
        return true;
    }
    [MemberNotNullWhen(false, nameof(CurrentException))]
    public static bool TryWarn<TWarning>(string message) where TWarning : PyExceptionType<TWarning>, ISharedInstance<TWarning>, new()
    {
        return TryWarn(TWarning.Shared, message);
    }
}

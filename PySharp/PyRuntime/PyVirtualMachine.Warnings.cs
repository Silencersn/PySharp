using PySharp.PyModules.Builtins;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PySharp.PyRuntime;

partial class PyVirtualMachine
{
    [MemberNotNullWhen(false, nameof(CurrentException))]
    public static bool TryWarn(PyExceptionType warningType, string message)
    {
        Console.WriteLine(warningType.Create(PyStrObject.FromString(message)).ToMessage());
        return true;
    }
    [MemberNotNullWhen(false, nameof(CurrentException))]
    public static bool TryWarn<TWarning>(string message) where TWarning : PyExceptionType<TWarning>, ISharedInstance<TWarning>, new()
    {
        return TryWarn(TWarning.Shared, message);
    }
}

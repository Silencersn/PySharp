using PySharp.PyModules.Builtins;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.PyRuntime.Calls;

partial class PyCallContext
{
    internal PyExceptionObject? CurrentException
    {
        get => State.CurrentException;
        set => State.CurrentException = value;
    }

    internal void ClearException()
    {
        CurrentException = null;
    }
}

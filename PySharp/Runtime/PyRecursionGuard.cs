using PySharp.Modules.Builtins;
using System.Runtime.CompilerServices;

namespace PySharp.Runtime;

// Recursions that enter no Python frame cannot be bounded by the frame
// counters: container comparison, container repr (print/str/f-string/format
// all enter through the same entry) and tuple hashing recurse in native code
// only. CPython bounds the same recursions with _Py_EnterRecursiveCall, which
// compares machine stack usage rather than counting calls, so these entry
// points probe the native stack instead; without the probe the .NET stack is
// exhausted and the process dies with an uncatchable StackOverflowException.
internal static class PyRecursionGuard
{
    // The RecursionError to propagate when the native stack is nearly
    // exhausted, or null when there is still room to recurse. The probe holds
    // back a reserve for the unwinding path, so the depth that still succeeds
    // stays below the depth that would overflow.
    internal static PyExceptionObject? ProbeNativeStack()
    {
        if (RuntimeHelpers.TryEnsureSufficientExecutionStack())
            return null;

        return PyRecursionErrorObjectType.Shared.Create(PyStrObject.FromString(PySR.Runtime_Recursion_MaxRecursionDepthExceeded));
    }
}

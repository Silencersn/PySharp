using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Threading;

public static partial class PyThreadingFunctions
{
    // threading.py's RLock is a factory function, not the _thread.RLock
    // type itself; the arguments are accepted (with a deprecation path)
    // and ignored
    [PyExport("RLock", nameof(RLockImpl))]
    public static partial PyBuiltinFunctionOrMethodObject RLock { get; }

    [PyFunctionParameters("*args", "**kwargs")]
    private static PyResult RLockImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments.ExtraArgs.Count is not 0 || arguments.ExtraKwargs.Count is not 0)
        {
            var warnResult = context.Warn(
                PyDeprecationWarningObjectType.Shared, PySR.Runtime_Threading_DeprecatedRLockArguments);
            if (warnResult.IsError)
                return warnResult;
        }

        return new PyRLockObject();
    }
}

using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Time;

public static partial class PyTimeFunctions
{
    [PyExport("time", nameof(TimeImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Time { get; }

    [PyFunctionParameters()]
    private static PyResult TimeImpl(PyCallContext context, PyArguments arguments)
    {
        var span = DateTime.UtcNow - DateTime.UnixEpoch;
        var seconds = span.TotalSeconds;
        return PyFloatObject.FromDouble(seconds);
    }

    [PyExport("sleep", nameof(SleepImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Sleep { get; }

    [PyFunctionParameters("secs", "/")]
    private static PyResult SleepImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Float(context, arguments[0]);
        if (result.IsError)
            return result;

        var secs = result.Value.Value;
        if (secs < 0)
            return PyResult.RaiseException(PyValueErrorObjectType.Shared, PySR.Runtime_Time_SleepNonNegative);

        // Thread.Sleep is millisecond-bounded; huge spans cap at the
        // Sleep ceiling instead of overflowing, mirroring CPython's
        // "sleep until interrupted" behavior for absurd durations
        var milliseconds = Math.Min(secs * 1000, uint.MaxValue - 1);
        Thread.Sleep(TimeSpan.FromMilliseconds(milliseconds));
        return PyNoneObject.None;
    }
}

using PySharp.Utility;

namespace PySharp.Runtime.Environments;

public sealed partial class PyEnvironment
{
    // Per-interpreter int<->str decimal conversion limit
    // (sys.set_int_max_str_digits; CPython keeps the value in
    // PyInterpreterState.long_state.max_str_digits).
    internal PyIntStrDigitsState IntStrDigits { get; } = new();
}

// The mutable per-environment half of the digit limit; PyIntStrDigitsLimit
// carries the constants and the pure checks over the value stored here.
internal sealed class PyIntStrDigitsState
{
    private int _maxStrDigits = PyIntStrDigitsLimit.DefaultMaxStrDigits;

    public int MaxStrDigits => _maxStrDigits;

    // False when the value is not a valid limit (CPython
    // _PySys_SetIntMaxStrDigits).
    public bool TrySetMaxStrDigits(int value)
    {
        if (!PyIntStrDigitsLimit.IsValidLimit(value))
            return false;

        _maxStrDigits = value;
        return true;
    }
}

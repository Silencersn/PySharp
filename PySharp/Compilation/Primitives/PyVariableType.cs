namespace PySharp.Compilation.Primitives;

public enum PyVariableType
{
    Unknown,
    Local,
    Global,
    Closure,
    Nonlocal = Closure,
    Parameter,

    // only appears during or after the semantic analysis phase
    CapturedLocal,
    CapturedParameter
}

namespace PySharp.Runtime.Environments;

public sealed partial class PyEnvironment
{
    // Per-interpreter warning policy.
    internal WarningState Warnings { get; } = new();
}

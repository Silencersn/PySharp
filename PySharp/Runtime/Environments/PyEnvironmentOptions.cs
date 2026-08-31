namespace PySharp.Runtime.Environments;

public sealed record class PyEnvironmentOptions
{
    public static PyEnvironmentOptions Default { get; } = new()
    {
        NotImplyImportSite = false,
        OptimizationLevel = 0,
    };

    public bool NotImplyImportSite { get; init; }
    public int OptimizationLevel { get; init; }
}

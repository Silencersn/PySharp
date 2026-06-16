namespace PySharp.Runtime.Environments;

public sealed record class PyEnvironmentOptions
{
    public static PyEnvironmentOptions Default { get; } = new()
    {
        NotImplyImportSite = false
    };

    public bool NotImplyImportSite { get; init; }
}

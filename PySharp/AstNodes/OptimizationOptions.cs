namespace PySharp.AstNodes;

public sealed class OptimizationOptions
{
    public static OptimizationOptions O0 { get; } = new()
    {
        Debug = true,
    };

    public static OptimizationOptions O1 { get; } = new()
    {
        Debug = false,
    };

    public bool Debug { get; init; }

    private OptimizationOptions()
    {
    }
}




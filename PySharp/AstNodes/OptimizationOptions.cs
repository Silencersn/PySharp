namespace PySharp.AstNodes;

public sealed class OptimizationOptions
{
    public static OptimizationOptions O0 { get; } = new()
    {
        NoOptimization = true,
        Debug = true,
    };

    public static OptimizationOptions O1 { get; } = new()
    {
        Debug = false,
        ConstantFolding = true,
        DeadCodeElimination = true,
        CodeCleanup = true,
        ShortCircuit = true,
    };

    internal bool NoOptimization { get; init; }
    public bool Debug { get; init; }
    public bool ConstantFolding { get; init; }
    public bool DeadCodeElimination { get; init; }
    public bool CodeCleanup { get; init; }
    public bool ShortCircuit { get; init; }

    private OptimizationOptions()
    {
    }
}




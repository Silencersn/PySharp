namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PyTypeAttribute : PyAttribute
{
    public PyTypeAttribute(string qualName)
    {
        QualName = qualName;
        Module = "builtins";
        IsSealed = false;
    }

    public string QualName { get; }
    public string? Module { get; set; }
    public bool IsSealed { get; set; }
}

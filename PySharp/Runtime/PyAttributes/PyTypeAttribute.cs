namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class PyTypeAttribute : PyAttribute
{
    public PyTypeAttribute(string qualName)
    {
        QualName = qualName;
        Module = "builtins";
    }

    public string QualName { get; }
    public string? Module { get; set; }
}

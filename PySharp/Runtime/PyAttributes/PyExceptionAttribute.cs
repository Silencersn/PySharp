namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PyExceptionAttribute : PyAttribute
{
    public Type[]? Bases { get; set; }
}

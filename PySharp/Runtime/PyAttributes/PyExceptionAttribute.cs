namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PyExceptionAttribute : PyTypeAttribute
{
    public PyExceptionAttribute(string qualName) : base(qualName) { }

    public Type[]? Bases { get; set; }
}

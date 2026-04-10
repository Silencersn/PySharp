namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PyStaticMethodAttribute : PyAttribute
{
    public PyStaticMethodAttribute(string name)
    {
        Name = name;
        Order = 1;
    }

    public string Name { get; }
    public int Order { get; set; }
}

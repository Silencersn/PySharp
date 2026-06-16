namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PyMethodAttribute : PyAttribute
{
    public PyMethodAttribute(string name)
    {
        Name = name;
        Order = 1;
    }

    public string Name { get; }
    public int Order { get; set; }
}

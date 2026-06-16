namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PyPropertyAttribute : PyAttribute
{
    public PyPropertyAttribute(string name)
    {
        Name = name;
        Type = PyPropertyMethodType.Getter;
    }

    public string Name { get; }
    public PyPropertyMethodType Type { get; set; }
}

public enum PyPropertyMethodType
{
    Getter = 0,
    Setter = 1,
    Deleter = 2,
}

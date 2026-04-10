namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class PyTypeConstructorAttribute : PyAttribute
{
    public PyTypeConstructorAttribute()
    {
        DoNotGenerateConstructor = false;
        AccessModifier = "private";
    }

    public bool DoNotGenerateConstructor { get; set; }
    public string AccessModifier { get; set; }
}


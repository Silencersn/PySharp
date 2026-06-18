namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
internal sealed class PySlotAttribute : PyAttribute
{
    public string? SlotsMember { get; set; }
}
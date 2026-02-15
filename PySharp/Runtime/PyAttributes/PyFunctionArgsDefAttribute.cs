namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PyFunctionArgsDefAttribute : PyAttribute
{
    public PyFunctionArgsDefAttribute(params string[] parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        Parameters = parameters;
    }

    public string[] Parameters { get; set; }
}

namespace PySharp.Runtime.PyAttributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PyFunctionParametersAttribute : PyAttribute
{
    public PyFunctionParametersAttribute(params string[] parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        Parameters = parameters;
    }

    public string[] Parameters { get; set; }
}

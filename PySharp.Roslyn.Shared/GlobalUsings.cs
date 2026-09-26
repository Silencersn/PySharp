global using Microsoft.CodeAnalysis;
global using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PySharp.SourceGeneration")]
[assembly: InternalsVisibleTo("PySharp.SourceGeneration.Internal")]

// Nullability-contract polyfills: netstandard2.0 predates these attributes, and
// netstandard2.0 must not depend on the System.Diagnostics.CodeAnalysis package.
namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
internal sealed class NotNullWhenAttribute : Attribute
{
    public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;
    public bool ReturnValue { get; }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
internal sealed class NotNullAttribute : Attribute;

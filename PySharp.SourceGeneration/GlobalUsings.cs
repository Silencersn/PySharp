global using Microsoft.CodeAnalysis;
global using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PySharp.SourceGeneration.Internal")]

namespace PySharp.SourceGeneration;

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
internal sealed class NotNullWhenAttribute : Attribute
{
    public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;
    public bool ReturnValue { get; }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
internal sealed class NotNullAttribute : Attribute;

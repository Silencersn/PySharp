using Microsoft.CodeAnalysis;
using System.Linq;

namespace PySharp.SourceGeneration.Diagnostics;

/// <summary>
/// A carrier for a single failed attribute-argument validation.
/// Created during the transform stage (captured only, not reported), then reported as a
/// <see cref="Diagnostic"/> via <see cref="Report"/> during the RegisterSourceOutput stage.
/// Immutable record with value equality so the incremental generator cache can hit.
/// </summary>
internal sealed record class DiagnosticInfo
{
    /// <summary>Maps to one of PYARG002/003/004.</summary>
    public DiagnosticDescriptor Descriptor { get; }

    /// <summary>Stringified values filling the messageFormat placeholders (for value equality).</summary>
    public string[] MessageArgs { get; }

    /// <summary>Location.None when the attribute application location cannot be resolved.</summary>
    public Location Location { get; }

    private DiagnosticInfo(DiagnosticDescriptor descriptor, string[] messageArgs, Location location)
    {
        Descriptor = descriptor;
        MessageArgs = messageArgs;
        Location = location;
    }

    public static DiagnosticInfo Argument(AttributeData attribute, string message)
    {
        var syntaxReference = attribute.ApplicationSyntaxReference;
        var location = syntaxReference?.SyntaxTree.GetLocation(syntaxReference.Span) ?? Location.None;
        return new DiagnosticInfo(PyGeneratorDiagnostics.InvalidArgument, [message], location);
    }

    public static DiagnosticInfo ArgumentNull(AttributeData attribute, string name)
    {
        return Argument(attribute, $"{name} cannot be null");
    }

    /// <summary>Required argument is null (PYARG002).</summary>
    public static DiagnosticInfo RequiredArgumentNull(
        AttributeData attribute,
        string argumentName,
        SyntaxReference? syntaxReference = null)
    {
        return new DiagnosticInfo(
            PyGeneratorDiagnostics.RequiredArgumentNull,
            new[] { argumentName },
            ResolveLocation(attribute, syntaxReference));
    }

    /// <summary>Enum argument value is outside the defined range (PYARG003).</summary>
    public static DiagnosticInfo InvalidEnumValue(
        AttributeData attribute,
        string argumentName,
        object enumValue,
        string enumTypeName,
        SyntaxReference? syntaxReference = null)
    {
        return new DiagnosticInfo(
            PyGeneratorDiagnostics.InvalidEnumValue,
            new[] { enumValue?.ToString() ?? string.Empty, enumTypeName, argumentName },
            ResolveLocation(attribute, syntaxReference));
    }

    /// <summary>Argument type does not match, or the constant is invalid (PYARG004).</summary>
    public static DiagnosticInfo InvalidArgumentType(
        AttributeData attribute,
        string argumentName,
        string expectedType,
        string actualType,
        SyntaxReference? syntaxReference = null)
    {
        return new DiagnosticInfo(
            PyGeneratorDiagnostics.InvalidArgumentType,
            new[] { argumentName, actualType, expectedType },
            ResolveLocation(attribute, syntaxReference));
    }

    public Diagnostic ToDiagnostic()
    {
        return Diagnostic.Create(Descriptor, Location, messageArgs: MessageArgs);
    }

    public DiagnosticOr<T> ToDiagnosticOr<T>() where T : class
    {
        return DiagnosticOr<T>.From(this);
    }

    /// <summary>Reports this error during the RegisterSourceOutput stage.</summary>
    public void Report(SourceProductionContext context)
    {
        context.ReportDiagnostic(ToDiagnostic());
    }

    private static Location ResolveLocation(AttributeData attribute, SyntaxReference? syntaxReference)
    {
        // Prefer the explicitly passed syntax reference, otherwise fall back to the attribute's own application reference.
        // Uses SyntaxTree.GetLocation(span) which does not need GetSyntax(), avoiding re-parsing the syntax node.
        syntaxReference ??= attribute.ApplicationSyntaxReference;
        return syntaxReference is not null
            ? syntaxReference.SyntaxTree.GetLocation(syntaxReference.Span)
            : Location.None;
    }
}

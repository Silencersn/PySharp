using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer;

partial class PySharpAnalyzer
{
    /// <summary>
    /// PYSP003 — Use <c>PySpecialNames.Interned.XXX</c> instead of
    /// <c>PyStrObject.InternPool.FromString(PySpecialNames.XXX)</c>.
    /// <para/>
    /// Triggers when <c>PyStrObject.InternPool.FromString</c> is called with a
    /// <c>PySpecialNames</c> constant as argument. The pre-interned field on
    /// <c>PySpecialNames.Interned</c> should be preferred because it avoids the
    /// lookup overhead and is more concise.
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// PySpecialNames.Interned.QualName
    /// </code>
    /// Non-compliant:
    /// <code>
    /// PyStrObject.InternPool.FromString(PySpecialNames.QualName)
    /// </code>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSP003 = new(
        nameof(PYSP003),
        "Use PySpecialNames.Interned field instead of InternPool.FromString",
        "Use 'PySpecialNames.Interned.{0}' instead of 'PyStrObject.InternPool.FromString(PySpecialNames.{0})'",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Prefer the pre-interned field on PySpecialNames.Interned over calling InternPool.FromString with a PySpecialNames constant.");

    private static void AnalyzeInternPoolFromString(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Resolve the method symbol
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        // Must be named "FromString" and be a static method
        if (methodSymbol.Name is not "FromString" || methodSymbol.MethodKind is not MethodKind.Ordinary)
            return;

        // Containing type must be PyStrObject.InternPool
        var containingType = methodSymbol.ContainingType;
        if (containingType is null)
            return;

        if (!IsPyStrObjectInternPoolType(containingType))
            return;

        // Must have exactly one argument
        if (invocation.ArgumentList.Arguments.Count is not 1)
            return;

        var argument = invocation.ArgumentList.Arguments[0].Expression;

        // Argument must be a member access: PySpecialNames.XXX
        if (argument is not MemberAccessExpressionSyntax memberAccess)
            return;

        // Resolve the left side to check it's PySpecialNames
        var leftType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        if (leftType is null)
            return;

        if (!IsPySpecialNamesType(leftType))
            return;

        // Get the member name for the message
        var memberName = memberAccess.Name.Identifier.ValueText;

        // Report diagnostic at the invocation
        context.ReportDiagnostic(Diagnostic.Create(
            PYSP003, invocation.GetLocation(), memberName));
    }

    private static bool IsPyStrObjectInternPoolType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        // The type is PyStrObject.InternPool (nested class)
        return namedType.Name is "InternPool" &&
               namedType.ContainingType?.Name is "PyStrObject" &&
               namedType.ContainingNamespace?.ToDisplayString() is "PySharp.Modules.Builtins";
    }

    private static bool IsPySpecialNamesType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        return namedType.Name is "PySpecialNames" &&
               namedType.ContainingNamespace?.ToDisplayString() is "PySharp.Runtime";
    }
}

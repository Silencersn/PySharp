using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer;

partial class PySharpAnalyzer
{
    /// <summary>
    /// PYSP001 — Use implicit conversion instead of <c>FromValue()</c>.
    /// <para/>
    /// Triggers when <c>return FromValue(x);</c> is used inside a method returning <c>PyResult</c> or <c>PyResult&lt;T&gt;</c>.
    /// The implicit conversion operator exists and should be preferred.
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// return x;
    /// </code>
    /// Non-compliant:
    /// <code>
    /// return FromValue(x);
    /// </code>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSP001 = new(
        nameof(PYSP001),
        "Use implicit conversion instead of FromValue()",
        "'FromValue()' is redundant; use implicit conversion instead",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Prefer implicit conversion over 'FromValue()' calls in return statements when the return type is PyResult or PyResult<T>.");

    private static void AnalyzeFromValueCall(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Resolve the method symbol
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        // Must be named "FromValue" and be a static method
        if (methodSymbol.Name is not "FromValue" || methodSymbol.MethodKind is not MethodKind.Ordinary)
            return;

        // Must be on PyResult or PyResult<T> in PySharp.Runtime.Calls namespace
        var containingType = methodSymbol.ContainingType;
        if (containingType is null)
            return;

        if (!IsPyResultType(containingType))
            return;

        // Must be directly inside a return statement's expression
        if (invocation.Parent is not ReturnStatementSyntax)
            return;

        // Get enclosing symbol (method, property, etc.) and check its return type
        var enclosingSymbol = context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart);
        if (enclosingSymbol is null)
            return;

        // Skip if inside an implicit/explicit conversion operator — those must call FromValue
        if (enclosingSymbol is IMethodSymbol { Name: "op_Implicit" or "op_Explicit" })
            return;

        ITypeSymbol? returnType = enclosingSymbol switch
        {
            IMethodSymbol m => m.ReturnType,
            IPropertySymbol p => p.Type,
            _ => null
        };

        if (returnType is null)
            return;

        // Check if the return type is PyResult or PyResult<T>
        if (!IsPyResultType(returnType))
            return;

        // Report diagnostic at the invocation (FromValue(...))
        context.ReportDiagnostic(Diagnostic.Create(
            PYSP001, invocation.GetLocation()));
    }

    private static bool IsPyResultType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        // Normalize: for generic types, compare against the open generic definition
        var checkType = namedType.IsGenericType ? namedType.OriginalDefinition : namedType;
        return checkType.Name is "PyResult" &&
               checkType.ContainingNamespace?.ToDisplayString() is "PySharp.Runtime.Calls";
    }
}

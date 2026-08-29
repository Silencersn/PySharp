using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer;

partial class PySharpAnalyzer
{
    /// <summary>
    /// PYSP005 — Return the result directly instead of '.ExceptionResult'.
    /// <para/>
    /// Triggers when <c>return x.ExceptionResult;</c> is used inside a method/property
    /// returning a non-generic <c>PyResult</c>, where <c>x</c> is also a non-generic
    /// <c>PyResult</c>. The implicit conversion from <c>PyExceptionResult</c> to
    /// <c>PyResult</c> collapses a successful result to <c>None</c>, so returning the
    /// result directly (<c>return x;</c>) is clearer and preserves the success value.
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// return x;
    /// </code>
    /// Non-compliant:
    /// <code>
    /// return x.ExceptionResult;
    /// </code>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSP005 = new(
        nameof(PYSP005),
        "Return result directly instead of .ExceptionResult",
        "'return {0}' directly instead of 'return {0}.ExceptionResult'",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When the return type is a non-generic PyResult, returning x.ExceptionResult collapses a successful result to None; prefer returning the result directly.");

    private static void AnalyzeExceptionResultReturn(SyntaxNodeAnalysisContext context)
    {
        var returnStatement = (ReturnStatementSyntax)context.Node;

        // Expression must be a member access: x.ExceptionResult
        if (returnStatement.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        // Right side must be "ExceptionResult"
        if (memberAccess.Name is not IdentifierNameSyntax { Identifier: { ValueText: "ExceptionResult" } })
            return;

        // Resolve the property symbol; must be the ExceptionResult property on PyResult
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol propertySymbol)
            return;

        if (propertySymbol.Name is not "ExceptionResult")
            return;

        var containingType = propertySymbol.ContainingType;
        if (containingType is null)
            return;

        if (!IsNonGenericPyResult(containingType))
            return;

        // The receiver (x) must also be a non-generic PyResult
        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        if (receiverType is null)
            return;

        if (!IsNonGenericPyResult(receiverType))
            return;

        // Get enclosing symbol and verify its return type is a non-generic PyResult
        var enclosingSymbol = context.SemanticModel.GetEnclosingSymbol(returnStatement.SpanStart);
        if (enclosingSymbol is null)
            return;

        // Skip inside an implicit/explicit conversion operator
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

        if (!IsNonGenericPyResult(returnType))
            return;

        // Report diagnostic at the member access (x.ExceptionResult), with x's text as arg
        context.ReportDiagnostic(Diagnostic.Create(
            PYSP005, memberAccess.GetLocation(), memberAccess.Expression.ToString()));
    }

    private static bool IsNonGenericPyResult(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        return !namedType.IsGenericType &&
               namedType.Name is "PyResult" &&
               namedType.ContainingNamespace?.ToDisplayString() is "PySharp.Runtime.Calls";
    }
}

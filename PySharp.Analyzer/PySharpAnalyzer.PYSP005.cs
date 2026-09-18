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
    /// Triggers when <c>x.ExceptionResult</c> is returned (<c>return x.ExceptionResult;</c>, an
    /// expression-bodied member <c>=&gt; x.ExceptionResult</c>, or an expression-bodied lambda) in a
    /// context where <c>return x;</c> compiles directly: the immediate enclosing symbol (method,
    /// accessor, local function, or anonymous function) returns a PySharp.Runtime.Calls
    /// PyResult-family type and <c>x</c>'s type is either the same type (non-generic
    /// <c>PyResult</c>→<c>PyResult</c>, <c>PyResult&lt;T&gt;</c>→<c>PyResult&lt;T&gt;</c>) or a
    /// generic <c>PyResult&lt;T&gt;</c> widening to non-generic <c>PyResult</c>. The detour through
    /// <c>PyExceptionResult</c> adds two implicit conversions and collapses a successful result to
    /// <c>default</c> (<c>None</c>), so returning the result directly is clearer and preserves the
    /// success value.
    /// <para/>
    /// Compliant (no diagnostic): cross-type bridges where <c>return x;</c> would not compile
    /// (e.g. a <c>PyResult</c> receiver in a <c>PyResult&lt;T&gt;</c>-returning member, or
    /// <c>PyResult&lt;T&gt;</c>→<c>PyResult&lt;U&gt;</c> with different type arguments), members
    /// returning the nested <c>PyResult.PyExceptionResult</c> type directly, conversion operators,
    /// and anything outside the immediate enclosing symbol's own return type (the immediate symbol
    /// is the anonymous function for returns inside lambdas).
    /// </summary>
    private static readonly DiagnosticDescriptor PYSP005 = new(
        nameof(PYSP005),
        "Return result directly instead of .ExceptionResult",
        "'return {0}' directly instead of 'return {0}.ExceptionResult'",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When 'return x;' compiles directly (same PyResult-family type, or PyResult<T> widening to PyResult), returning x.ExceptionResult adds two conversions and collapses a successful result to default; prefer returning the result directly.");

    private static void AnalyzeExceptionResultReturn(SyntaxNodeAnalysisContext context)
    {
        ExpressionSyntax? expression = context.Node switch
        {
            ReturnStatementSyntax returnStatement => returnStatement.Expression,
            ArrowExpressionClauseSyntax arrowClause => arrowClause.Expression,
            SimpleLambdaExpressionSyntax { Body: ExpressionSyntax simpleBody } => simpleBody,
            ParenthesizedLambdaExpressionSyntax { Body: ExpressionSyntax lambdaBody } => lambdaBody,
            _ => null
        };

        // Expression must be a member access: x.ExceptionResult
        if (expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        // Right side must be "ExceptionResult"
        if (memberAccess.Name is not IdentifierNameSyntax { Identifier: { ValueText: "ExceptionResult" } })
            return;

        // Resolve the property symbol; must belong to PyResult / PyResult<T>
        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol propertySymbol)
            return;

        if (propertySymbol.Name is not "ExceptionResult")
            return;

        if (!IsPyResultFamily(propertySymbol.ContainingType.OriginalDefinition))
            return;

        // The receiver (x) must be a PyResult-family type as well
        var receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
        if (receiverType is not INamedTypeSymbol receiverNamed)
            return;

        if (!IsPyResultFamily(receiverNamed))
            return;

        // The immediate enclosing symbol's return type decides whether 'return x;' compiles
        var enclosingSymbol = context.SemanticModel.GetEnclosingSymbol(memberAccess.SpanStart, context.CancellationToken);
        if (enclosingSymbol is null)
            return;

        // Skip inside an implicit/explicit conversion operator
        if (enclosingSymbol is IMethodSymbol { Name: "op_Implicit" or "op_Explicit" })
            return;

        ITypeSymbol? returnType = enclosingSymbol switch
        {
            IMethodSymbol method => method.ReturnType,
            IPropertySymbol property => property.Type,
            _ => null
        };

        if (returnType is not INamedTypeSymbol returnNamed)
            return;

        if (!IsPyResultFamily(returnNamed))
            return;

        // 'return x;' must compile directly: same type, or PyResult<T> widening to PyResult
        var sameType = SymbolEqualityComparer.Default.Equals(receiverNamed, returnNamed);
        var widensToNonGeneric = receiverNamed.IsGenericType && !returnNamed.IsGenericType;

        if (!sameType && !widensToNonGeneric)
            return;

        // Report diagnostic at the member access (x.ExceptionResult), with x's text as arg
        context.ReportDiagnostic(Diagnostic.Create(
            PYSP005, memberAccess.GetLocation(), memberAccess.Expression.ToString()));
    }

    private static bool IsPyResultFamily(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        return namedType.Name is "PyResult" &&
               namedType.ContainingNamespace?.ToDisplayString() is "PySharp.Runtime.Calls";
    }
}

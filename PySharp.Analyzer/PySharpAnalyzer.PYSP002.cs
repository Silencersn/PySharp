using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer;

partial class PySharpAnalyzer
{
    /// <summary>
    /// PYSP002 — Use <c>context.Comparer</c> instead of <c>PyObjectComparer.Default</c>
    /// when a <c>PyCallContext</c> parameter is available.
    /// <para/>
    /// Triggers when <c>PyObjectComparer.Default</c> is used inside a method that has a
    /// <c>PyCallContext</c> parameter (nullable or non-nullable). The context-bound comparer
    /// should be preferred because <c>PyObjectComparer.Default</c> uses a sentinel context
    /// that lacks <c>FrameState</c> and will throw on certain code paths.
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// context.Comparer
    /// context?.Comparer ?? PyObjectComparer.Default   // when context is nullable
    /// </code>
    /// Non-compliant:
    /// <code>
    /// PyObjectComparer.Default
    /// </code>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSP002 = new(
        nameof(PYSP002),
        "Use context.Comparer instead of PyObjectComparer.Default",
        "Use '{0}' instead of 'PyObjectComparer.Default'",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When a PyCallContext parameter is available, use its Comparer property instead of PyObjectComparer.Default to ensure proper FrameState access.");

    private static void AnalyzeDefaultComparerUse(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Right side must be "Default"
        if (memberAccess.Name is not IdentifierNameSyntax { Identifier: { ValueText: "Default" } })
            return;

        // Left side must resolve to PyObjectComparer type
        if (memberAccess.Expression is not ExpressionSyntax leftExpr)
            return;

        var leftType = context.SemanticModel.GetTypeInfo(leftExpr, context.CancellationToken).Type;
        if (leftType is null)
            return;

        if (!IsPyObjectComparerType(leftType))
            return;

        // Walk up the syntax tree to find the enclosing method, constructor, or local function
        // Skip lambda/anonymous function nodes — they can close over the outer method's parameters
        SyntaxNode? enclosingDeclaration = memberAccess.FirstAncestorOrSelf<SyntaxNode>(node =>
            node is MethodDeclarationSyntax ||
            node is ConstructorDeclarationSyntax ||
            node is LocalFunctionStatementSyntax);

        if (enclosingDeclaration is null)
            return;

        // Get the method symbol
        IMethodSymbol? methodSymbol = enclosingDeclaration switch
        {
            MethodDeclarationSyntax m => context.SemanticModel.GetDeclaredSymbol(m, context.CancellationToken) as IMethodSymbol,
            ConstructorDeclarationSyntax c => context.SemanticModel.GetDeclaredSymbol(c, context.CancellationToken) as IMethodSymbol,
            LocalFunctionStatementSyntax lf => context.SemanticModel.GetDeclaredSymbol(lf, context.CancellationToken) as IMethodSymbol,
            _ => null
        };

        if (methodSymbol is null)
            return;

        // Find the first PyCallContext parameter
        IParameterSymbol? pyCallContextParam = null;
        foreach (var param in methodSymbol.Parameters)
        {
            if (IsPyCallContextType(param.Type))
            {
                pyCallContextParam = param;
                break;
            }
        }

        if (pyCallContextParam is null)
            return;

        // Determine the suggested replacement based on nullability
        string paramName = pyCallContextParam.Name;

        // Skip if this is already the safe fallback pattern: {param}?.Comparer ?? PyObjectComparer.Default
        if (pyCallContextParam.NullableAnnotation is NullableAnnotation.Annotated
            && memberAccess.Parent is BinaryExpressionSyntax coalesceExpr
            && coalesceExpr.Kind() is SyntaxKind.CoalesceExpression
            && coalesceExpr.Right == memberAccess
            && coalesceExpr.Left is ConditionalAccessExpressionSyntax condAccess
            && condAccess.Expression is IdentifierNameSyntax condId
            && condId.Identifier.ValueText == paramName
            && condAccess.WhenNotNull is MemberBindingExpressionSyntax memberBinding
            && memberBinding.Name.Identifier.ValueText == "Comparer")
        {
            return;
        }

        string suggestion = pyCallContextParam.NullableAnnotation is NullableAnnotation.Annotated
            ? $"{paramName}?.Comparer ?? PyObjectComparer.Default"
            : $"{paramName}.Comparer";

        // Report diagnostic at the member access (PyObjectComparer.Default)
        context.ReportDiagnostic(Diagnostic.Create(
            PYSP002, memberAccess.GetLocation(), suggestion));
    }

    private static bool IsPyObjectComparerType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        return namedType.Name == "PyObjectComparer" &&
               namedType.ContainingNamespace?.ToDisplayString() == "PySharp.Runtime.Comparison";
    }

    private static bool IsPyCallContextType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        return namedType.Name == "PyCallContext" &&
               namedType.ContainingNamespace?.ToDisplayString() == "PySharp.Runtime.Calls";
    }
}

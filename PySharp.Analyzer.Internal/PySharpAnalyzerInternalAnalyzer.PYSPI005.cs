using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer.Internal;

partial class PySharpAnalyzerInternalAnalyzer
{
    /// <summary>
    /// PYSPI005 — Use <c>string.Empty</c> instead of <c>""</c>.
    /// <para/>
    /// Triggers when the empty string literal <c>""</c> is used.
    /// Exempted when it appears inside a constant pattern (e.g., <c>x is ""</c>,
    /// <c>x is not ""</c>, <c>case "":</c>).
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// return string.Empty;
    /// var s = string.Empty;
    /// if (s is "") ...
    /// </code>
    /// Non-compliant:
    /// <code>
    /// return "";
    /// var s = "";
    /// if (s == "") ...
    /// SomeMethod("");
    /// </code>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSPI005 = new(
        nameof(PYSPI005),
        "Use string.Empty instead of \"\"",
        "Use 'string.Empty' instead of \"\"",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Prefer 'string.Empty' over the empty string literal \"\" for consistency.");

    private static void AnalyzeEmptyStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;

        // Only flag empty string literals: ""
        if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
            return;

        if (literal.Token.ValueText != string.Empty)
            return;

        // Skip when inside a constant pattern (e.g., x is "", case "")
        if (IsInsideConstantPattern(literal))
            return;

        context.ReportDiagnostic(Diagnostic.Create(PYSPI005, literal.GetLocation()));
    }

    /// <summary>
    /// Returns true when the literal is inside a <c>ConstantPatternSyntax</c>,
    /// meaning it's used in an <c>is</c> / <c>is not</c> pattern or a <c>case</c> label.
    /// </summary>
    private static bool IsInsideConstantPattern(LiteralExpressionSyntax literal)
    {
        for (var current = literal.Parent; current is not null; current = current.Parent)
        {
            if (current is ConstantPatternSyntax)
                return true;

            // Stop at the statement/expression level — no need to climb further
            if (current is StatementSyntax or ExpressionSyntax)
                break;
        }

        return false;
    }
}

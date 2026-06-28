using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer.Internal;

partial class PySharpAnalyzerInternalAnalyzer
{
    /// <summary>
    /// PYSPI004 — Multi-line bare statement body should use braces.
    /// <para/>
    /// Triggers when the body of an <c>if</c>/<c>for</c>/<c>foreach</c>/<c>while</c>
    /// is a bare statement (not a <c>BlockSyntax</c>) that spans multiple lines.
    /// A multi-line body should always use braces for clarity.
    /// <para/>
    /// Compliant:
    /// <code>
    /// if (a)
    ///     return b;
    ///
    /// if (a)
    /// {
    ///     return VeryLong(
    ///         arg1, arg2);
    /// }
    /// </code>
    /// Non-compliant:
    /// <code>
    /// if (a)
    ///     return VeryLong(
    ///         arg1, arg2);
    /// </code>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSPI004 = new(
        nameof(PYSPI004),
        "Multi-line bare statement body should use braces",
        "Add braces to this multi-line body",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A control flow body that spans multiple lines should use braces for clarity.");

    private static void AnalyzeMultiLineBareBody(SyntaxNodeAnalysisContext context)
    {
        var statement = context.Node switch
        {
            IfStatementSyntax s => s.Statement,
            ForStatementSyntax s => s.Statement,
            ForEachStatementSyntax s => s.Statement,
            WhileStatementSyntax s => s.Statement,
            _ => null,
        };

        if (statement is null or BlockSyntax)
            return;

        var span = statement.GetLocation().GetLineSpan();
        if (span.StartLinePosition.Line != span.EndLinePosition.Line)
            context.ReportDiagnostic(Diagnostic.Create(PYSPI004, statement.GetLocation()));
    }
}

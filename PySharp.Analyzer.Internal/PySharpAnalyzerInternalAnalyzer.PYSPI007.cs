using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer.Internal;

partial class PySharpAnalyzerInternalAnalyzer
{
    /// <summary>
    /// PYSPI007 — Opening brace must be on a new line.
    /// <para/>
    /// Triggers when a non-empty <c>BlockSyntax</c> has its opening brace <c>{</c>
    /// on the same line as the preceding token, or on the same line as the first
    /// statement inside the block.  Empty blocks where both braces are on the
    /// same line (<c>{ }</c>) are exempted.
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// if (x)
    /// {
    ///     return b;
    /// }
    ///
    /// void Foo()
    /// {
    /// }
    ///
    /// if (x) { }
    /// </code>
    /// Non-compliant:
    /// <code>
    /// if (x) {
    ///     return b;
    /// }
    ///
    /// if (x)
    /// { return b; }
    ///
    /// void Foo() {
    /// }
    /// </code>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSPI007 = new(
        nameof(PYSPI007),
        "Opening brace must be on a new line",
        "Place the opening brace on a new line",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "All opening braces should be on a new line (Allman style). Empty blocks ({ }) on the same line are exempted.");

    private static void AnalyzeBlock(SyntaxNodeAnalysisContext context)
    {
        var block = (BlockSyntax)context.Node;
        var openBrace = block.OpenBraceToken;
        var closeBrace = block.CloseBraceToken;

        var openLine = openBrace.GetLocation().GetLineSpan().StartLinePosition.Line;
        var closeLine = closeBrace.GetLocation().GetLineSpan().StartLinePosition.Line;

        // Exception: empty block where { and } are on the same line, e.g. { }
        if (openLine == closeLine)
            return;

        // Check A: Is { on the same line as the preceding token?
        //   e.g. "if (x) { ... }" → { should be on a new line
        var prevToken = openBrace.GetPreviousToken();
        var prevLine = prevToken.GetLocation().GetLineSpan().StartLinePosition.Line;
        if (openLine == prevLine)
        {
            context.ReportDiagnostic(Diagnostic.Create(PYSPI007, openBrace.GetLocation()));
            return;
        }

        // Check B: Is content on the same line as {? (only for non-empty blocks)
        //   e.g. "if (x)\n{ return; }" → { should be on its own line
        if (block.Statements.Count > 0)
        {
            var firstStmtLine = block.Statements[0]
                .GetLocation().GetLineSpan().StartLinePosition.Line;
            if (firstStmtLine == openLine)
            {
                context.ReportDiagnostic(Diagnostic.Create(PYSPI007, openBrace.GetLocation()));
            }
        }
    }
}

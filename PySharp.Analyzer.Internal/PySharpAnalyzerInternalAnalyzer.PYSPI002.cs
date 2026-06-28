using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer.Internal;

partial class PySharpAnalyzerInternalAnalyzer
{
    /// <summary>
    /// PYSPI002 — Control flow body should be on a new line without braces.
    /// <para/>
    /// Triggers when: (a) a single-statement block uses unnecessary braces;
    /// (b) the statement is on the same line as the control flow keyword.
    /// Applies to: <c>if</c>/<c>for</c>/<c>foreach</c>/<c>while</c>.
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// if (a)
    ///     return b;
    /// for (int i = 0; i &lt; n; i++)
    ///     Process(i);
    /// </code>
    /// Non-compliant:
    /// <code>
    /// if (a) { return b; }
    /// if (a) return b;
    /// if (a)
    /// {
    ///     return b;
    /// }
    /// </code>
    /// Exemptions (no diagnostic):
    /// <list type="bullet">
    ///   <item><description>Single statement is itself a control flow statement (e.g., <c>if (a) { if (b) return c; }</c>) — removing braces would create ambiguity.</description></item>
    ///   <item><description>Single statement spans multiple lines (ternary, LINQ chain) — braces improve readability.</description></item>
    ///   <item><description>If-else chain where another branch needs braces (handled by PYSPI003 for consistency).</description></item>
    /// </list>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSPI002 = new(
        nameof(PYSPI002),
        "Body of control flow statement should be on a new line without braces",
        "Body of '{0}' should be on a new line without braces: {1}",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Control flow statements should have their body on a separate line without braces (e.g., 'if (a)\\n    return b;').");

    private static void AnalyzeControlFlowBody(SyntaxNodeAnalysisContext context)
    {
        var (keywordToken, statement) = context.Node switch
        {
            IfStatementSyntax s => (s.IfKeyword, (SyntaxNode?)s.Statement),
            ForStatementSyntax s => (s.ForKeyword, s.Statement),
            ForEachStatementSyntax s => (s.ForEachKeyword, s.Statement),
            WhileStatementSyntax s => (s.WhileKeyword, s.Statement),
            _ => (default, null),
        };

        if (statement is null)
            return;

        var keywordLine = keywordToken.GetLocation().GetLineSpan().StartLinePosition.Line;

        if (statement is BlockSyntax block)
        {
            // Single-statement block should omit braces: if (a) { return b; } or if (a)\n{\n    return b;\n}
            // But not when the single statement is itself a control flow statement (nested if/for/etc.),
            // because removing braces would create ambiguity.
            // Also not when this if-statement is part of a chain where another branch
            // has a multi-statement block — braces are needed for consistency (warning 3 covers this).
            if (block.Statements.Count is 1
                && !BlockNeedsBraces(block)
                && !IsBranchInMultiStatementChain(context.Node))
            {
                var sourceText = GetSourceSnippet(context.Node);
                context.ReportDiagnostic(Diagnostic.Create(
                    PYSPI002, block.GetLocation(), GetKeywordText(context.Node), sourceText));
            }
            return;
        }

        // Direct statement on same line as keyword: if (a) return b;
        var stmtLine = statement.GetLocation().GetLineSpan().StartLinePosition.Line;
        if (stmtLine == keywordLine)
        {
            var sourceText = GetSourceSnippet(context.Node);
            context.ReportDiagnostic(Diagnostic.Create(
                PYSPI002, statement.GetLocation(), GetKeywordText(context.Node), sourceText));
        }
    }

    private static string GetKeywordText(SyntaxNode node)
    {
        return node switch
        {
            IfStatementSyntax _ => "if",
            ForStatementSyntax _ => "for",
            ForEachStatementSyntax _ => "foreach",
            WhileStatementSyntax _ => "while",
            _ => "statement",
        };
    }

    private static string GetSourceSnippet(SyntaxNode node)
    {
        var text = node.ToString();
        // Truncate long snippets to avoid excessively long messages
        const int maxLen = 60;
        if (text.Length > maxLen)
            text = text.Substring(0, maxLen - 3) + "...";
        return text;
    }

    private static bool IsBranchInMultiStatementChain(SyntaxNode node)
    {
        // Walk up to the top-level if in an if-else-if chain
        while (node is IfStatementSyntax { Parent: ElseClauseSyntax { Parent: IfStatementSyntax parent } })
            node = parent;

        // Walk the chain, check if any branch has a block that cannot lose braces
        for (var current = (IfStatementSyntax)node; current is not null; current = current.Else?.Statement as IfStatementSyntax)
        {
            if (BranchNeedsBraces(current.Statement))
                return true;

            if (BranchNeedsBraces(current.Else?.Statement))
                return true;
        }

        return false;
    }

    private static bool BranchNeedsBraces(SyntaxNode? statement) =>
        statement is BlockSyntax block && BlockNeedsBraces(block);

    private static bool BlockNeedsBraces(BlockSyntax block) => block.Statements.Count switch
    {
        > 1 => true,
        1 => IsControlFlowStmt(block.Statements[0])
             || block.Statements[0].GetLocation().GetLineSpan() is var s
             && s.StartLinePosition.Line != s.EndLinePosition.Line,
        _ => false
    };

    private static bool IsControlFlowStmt(StatementSyntax s) => s
        is IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax
        or WhileStatementSyntax or DoStatementSyntax
        or LockStatementSyntax or UsingStatementSyntax or FixedStatementSyntax;
}

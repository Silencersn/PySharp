using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer.Internal;

partial class PySharpAnalyzerInternalAnalyzer
{
    /// <summary>
    /// PYSPI003 — Inconsistent brace style in an if-else chain.
    /// <para/>
    /// Triggers when at least one branch requires braces (multi-statement block, nested
    /// control flow, or multi-line statement) while other branches omit them.
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// // No braces at all (all branches are single-line without blocks)
    /// if (a) return b;
    /// else if (c) return d;
    /// else return e;
    ///
    /// // All branches use braces (consistent)
    /// if (a)
    /// {
    ///     return b;
    /// }
    /// else
    /// {
    ///     Do();
    ///     return d;
    /// }
    /// </code>
    /// Non-compliant:
    /// <code>
    /// // First branch lacks braces, else-if branch has a nested control-flow block
    /// if (a)
    ///     return b;
    /// else if (c)
    /// {
    ///     if (d) return e;
    /// }
    /// </code>
    /// Edge cases:
    /// <list type="bullet">
    ///   <item><description>All branches are single-statement blocks — PYSPI002 fires instead.</description></item>
    ///   <item><description>All branches are single-line without blocks — no diagnostic.</description></item>
    ///   <item><description>All branches consistently use or omit braces — no diagnostic.</description></item>
    /// </list>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSPI003 = new(
        nameof(PYSPI003),
        "Inconsistent brace style in if-else chain",
        "Inconsistent brace style in if-else chain - all branches should use braces: {0}",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "If any branch of an if-else chain has a multi-line body, all branches should consistently use braces.");

    private static void AnalyzeInconsistentIfChain(SyntaxNodeAnalysisContext context)
    {
        var ifStmt = (IfStatementSyntax)context.Node;

        // Only analyze from the top-level if, skip else-if nodes
        if (ifStmt.Parent is ElseClauseSyntax)
            return;

        // Collect all branch bodies (if + else-if + else)
        var branches = new List<SyntaxNode?>();
        for (var current = ifStmt; current is not null; )
        {
            branches.Add(current.Statement);
            if (current.Else?.Statement is IfStatementSyntax next)
            {
                current = next;
                continue;
            }

            if (current.Else is not null)
                branches.Add(current.Else.Statement);

            break;
        }

        bool hasMultiBlock = branches.Any(b => b is BlockSyntax block && BlockNeedsBraces(block));
        bool hasNonBlock = branches.Any(b => b is not BlockSyntax);

        if (hasMultiBlock && hasNonBlock)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                PYSPI003, ifStmt.IfKeyword.GetLocation(), GetSourceSnippet(ifStmt)));
        }
    }
}

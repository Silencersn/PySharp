using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer.Internal;

partial class PySharpAnalyzerInternalAnalyzer
{
    /// <summary>
    /// PYSPI008 — Use semicolon syntax for empty type declarations.
    /// <para/>
    /// Triggers when a <c>class</c>, <c>struct</c>, <c>interface</c>, or <c>record</c>
    /// has an empty body (<c>{ }</c>).  These should use the C# 10+ file-scoped syntax
    /// (<c>class Foo;</c>) instead.
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// class Foo;
    /// struct Bar;
    /// interface IBaz;
    /// record SomeRec;
    ///
    /// class Foo { int x; }
    /// </code>
    /// Non-compliant:
    /// <code>
    /// class Foo { }
    /// struct Bar { }
    /// interface IBaz { }
    /// record SomeRec { }
    /// [SomeAttr] class Foo { }
    /// </code>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSPI008 = new(
        nameof(PYSPI008),
        "Use semicolon syntax for empty type declaration",
        "Use '{0};' instead of '{0} {{ }}'",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "C# 10+ allows file-scoped type declarations. Use 'class Foo;' instead of 'class Foo { }' for empty types.");

    private static void AnalyzeEmptyTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;

        // Skip if the type has members — it's not empty
        if (typeDecl.Members.Count > 0)
            return;

        // Skip if already a file-scoped type (already uses ; syntax, no braces)
        // Note: for file-scoped types, CloseBraceToken.Kind() is SyntaxKind.None,
        // not SyntaxKind.CloseBraceToken. IsMissing doesn't work here because
        // the parser doesn't insert an error-recovery token for this syntax.
        if (typeDecl.CloseBraceToken.Kind() != SyntaxKind.CloseBraceToken)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            PYSPI008,
            typeDecl.CloseBraceToken.GetLocation(),
            $"{typeDecl.Keyword.Text} {typeDecl.Identifier.Text}"));
    }
}

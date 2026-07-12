using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer.Internal;

partial class PySharpAnalyzerInternalAnalyzer
{
    /// <summary>
    /// PYSPI006 — Use <c>PySpecialNames</c> constant instead of a <c>"__xxx__"</c> literal.
    /// <para/>
    /// Triggers when any string literal matching the pattern <c>"__xxx__"</c> appears
    /// inside the <c>PySharp.Modules.Builtins</c> namespace. All such magic strings
    /// should be defined as constants in <c>PySpecialNames</c> and referenced via
    /// <c>PySpecialNames.Xxx</c>.
    /// <para/>
    /// Non-compliant:
    /// <code>
    /// [PyProperty("__defaults__")]
    /// var x = "__dict__";
    /// SomeMethod("__class__");
    /// </code>
    /// Compliant:
    /// <code>
    /// [PyProperty(PySpecialNames.Defaults)]
    /// var x = PySpecialNames.Dict;
    /// SomeMethod(PySpecialNames.Class);
    /// </code>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSPI006 = new(
        nameof(PYSPI006),
        "Use PySpecialNames constant instead of '__xxx__' literal",
        "Replace literal \"{0}\" with the corresponding PySpecialNames constant. If none exists, define one in PySpecialNames.cs",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Magic string literals wrapped in double underscores should be defined as constants in PySpecialNames.");

    private static void AnalyzeDunderStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;

        if (literal.Token.ValueText is not { } text)
            return;

        // Must match the __xxx__ pattern (at least __x__)
        if (text.Length <= 4 || !text.StartsWith("__") || !text.EndsWith("__"))
            return;

        // Only flag inside PySharp.Modules.Builtins namespace
        if (!IsInBuiltinsNamespace(context))
            return;

        context.ReportDiagnostic(Diagnostic.Create(PYSPI006, literal.GetLocation(), text));
    }

    /// <summary>
    /// Determines whether the current node is inside the <c>PySharp.Modules.Builtins</c> namespace
    /// by walking up the syntax tree to the nearest type declaration and inspecting its symbol.
    /// </summary>
    private static bool IsInBuiltinsNamespace(SyntaxNodeAnalysisContext context)
    {
        for (var current = context.Node.Parent; current is not null; current = current.Parent)
        {
            if (current is BaseTypeDeclarationSyntax typeDecl)
            {
                var symbol = context.SemanticModel.GetDeclaredSymbol(typeDecl);
                return symbol?.ContainingNamespace?.ToDisplayString() == "PySharp.Modules.Builtins";
            }
        }

        return false;
    }
}

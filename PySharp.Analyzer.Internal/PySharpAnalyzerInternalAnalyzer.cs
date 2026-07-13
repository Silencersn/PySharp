using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace PySharp.Analyzer.Internal;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public partial class PySharpAnalyzerInternalAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PYSPI001, PYSPI002, PYSPI003, PYSPI004, PYSPI005, PYSPI006, PYSPI007);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeConstantComparison, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeControlFlowBody,
            SyntaxKind.IfStatement,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,
            SyntaxKind.WhileStatement);
        context.RegisterSyntaxNodeAction(AnalyzeInconsistentIfChain, SyntaxKind.IfStatement);
        context.RegisterSyntaxNodeAction(AnalyzeMultiLineBareBody,
            SyntaxKind.IfStatement,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,
            SyntaxKind.WhileStatement);
        context.RegisterSyntaxNodeAction(AnalyzeEmptyStringLiteral, SyntaxKind.StringLiteralExpression);
        context.RegisterSyntaxNodeAction(AnalyzeDunderStringLiteral, SyntaxKind.StringLiteralExpression);
        context.RegisterSyntaxNodeAction(AnalyzeBlock, SyntaxKind.Block);
    }
}
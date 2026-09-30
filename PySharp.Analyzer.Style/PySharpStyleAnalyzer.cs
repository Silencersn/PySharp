using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace PySharp.Analyzer.Style;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public partial class PySharpStyleAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PYSPS001, PYSPS002, PYSPS003, PYSPS004, PYSPS005, PYSPS006, PYSPS007);

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
        context.RegisterSyntaxNodeAction(AnalyzeBlock, SyntaxKind.Block);
        context.RegisterSyntaxNodeAction(AnalyzeEmptyTypeDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration);
    }
}

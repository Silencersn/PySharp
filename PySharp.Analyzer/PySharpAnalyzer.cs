using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace PySharp.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public partial class PySharpAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(PYSP001, PYSP002, PYSP003, PYSP004, PYSP005);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeFromValueCall,
            SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeDefaultComparerUse,
            SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInternPoolFromString,
            SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeFactoryToConstant,
            SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeExceptionResultReturn,
            SyntaxKind.ReturnStatement);
    }
}

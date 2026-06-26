using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace PySharp.Analyzer.Internal
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PySharpAnalyzerInternalAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "PYSPI001";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Use pattern matching for constant comparisons",
            "Use '{0}' instead of '{1}'",
            "PySharp",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Use 'is' pattern matching (e.g., 'is null', 'is true', 'is 0') instead of '==' or '!=' with constants.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeConstantComparison, SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression);
        }

        private static void AnalyzeConstantComparison(SyntaxNodeAnalysisContext context)
        {
            var binaryExpr = (BinaryExpressionSyntax)context.Node;

            // Find the literal constant side (if any)
            LiteralExpressionSyntax? literal = binaryExpr.Left as LiteralExpressionSyntax
                ?? binaryExpr.Right as LiteralExpressionSyntax;

            if (literal == null)
                return;

            var literalText = literal.Token.Text;
            var isEquals = binaryExpr.IsKind(SyntaxKind.EqualsExpression);

            var oldOp = isEquals ? $"== {literalText}" : $"!= {literalText}";
            var newOp = isEquals ? $"is {literalText}" : $"is not {literalText}";

            var diagnostic = Diagnostic.Create(
                Rule,
                binaryExpr.OperatorToken.GetLocation(),
                newOp,
                oldOp);

            context.ReportDiagnostic(diagnostic);
        }
    }
}

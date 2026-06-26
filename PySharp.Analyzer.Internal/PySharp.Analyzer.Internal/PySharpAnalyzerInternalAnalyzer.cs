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
        public const string DiagnosticId2 = "PYSPI002";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Use pattern matching for constant comparisons",
            "Use '{0}' instead of '{1}'",
            "PySharp",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Use 'is' pattern matching (e.g., 'is null', 'is true', 'is 0') instead of '==' or '!=' with constants.");

        private static readonly DiagnosticDescriptor Rule2 = new DiagnosticDescriptor(
            DiagnosticId2,
            "Body of control flow statement should be on a new line without braces",
            "Body of '{0}' should be on a new line without braces: {1}",
            "PySharp",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Control flow statements should have their body on a separate line without braces (e.g., 'if (a)\\n    return b;').");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule, Rule2);

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
        }

        private static void AnalyzeConstantComparison(SyntaxNodeAnalysisContext context)
        {
            var binaryExpr = (BinaryExpressionSyntax)context.Node;

            // Find which side is a compile-time constant
            var isLeftConst = IsConstant(binaryExpr.Left, context.SemanticModel);
            var isRightConst = IsConstant(binaryExpr.Right, context.SemanticModel);

            ExpressionSyntax? constExpr;
            ExpressionSyntax targetExpr;

            if (isLeftConst)
            {
                constExpr = binaryExpr.Left;
                targetExpr = binaryExpr.Right;
            }
            else if (isRightConst)
            {
                constExpr = binaryExpr.Right;
                targetExpr = binaryExpr.Left;
            }
            else
            {
                return;
            }

            // Check the target type supports constant patterns
            var type = context.SemanticModel.GetTypeInfo(targetExpr).Type;
            if (type is null)
                return;

            // Unwrap Nullable<T>
            var underlying = type.OriginalDefinition?.SpecialType is SpecialType.System_Nullable_T
                ? ((INamedTypeSymbol)type).TypeArguments[0]
                : type;

            if (underlying.TypeKind is not TypeKind.Enum && !IsSpecialTypeWithConstantSupport(underlying.SpecialType))
                return;

            // For bare non-null literals, verify the literal type is compatible with the target type
            if (constExpr is LiteralExpressionSyntax bareLiteral
                && bareLiteral.Kind() is not SyntaxKind.NullLiteralExpression)
            {
                var litType = context.SemanticModel.GetTypeInfo(bareLiteral).Type;
                if (litType is not null && !IsIntegralType(underlying.SpecialType))
                {
                    var conversion = context.Compilation.ClassifyConversion(litType, underlying);
                    if (!conversion.IsImplicit && !conversion.IsIdentity)
                        return;
                }
            }

            var constText = constExpr.ToString();
            var isEquals = binaryExpr.IsKind(SyntaxKind.EqualsExpression);
            var oldOp = isEquals ? $"== {constText}" : $"!= {constText}";
            var newOp = isEquals ? $"is {constText}" : $"is not {constText}";

            context.ReportDiagnostic(Diagnostic.Create(
                Rule, binaryExpr.OperatorToken.GetLocation(), newOp, oldOp));
        }

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
                if (block.Statements.Count == 1 && !IsControlFlowWithEmbeddedStatement(block.Statements[0]))
                {
                    var sourceText = GetSourceSnippet(context.Node);
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rule2, block.GetLocation(), GetKeywordText(context.Node), sourceText));
                }
                return;
            }

            // Direct statement on same line as keyword: if (a) return b;
            var stmtLine = statement.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (stmtLine == keywordLine)
            {
                var sourceText = GetSourceSnippet(context.Node);
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule2, statement.GetLocation(), GetKeywordText(context.Node), sourceText));
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

        private static bool IsControlFlowWithEmbeddedStatement(StatementSyntax statement)
        {
            return statement is IfStatementSyntax
                || statement is ForStatementSyntax
                || statement is ForEachStatementSyntax
                || statement is WhileStatementSyntax
                || statement is DoStatementSyntax
                || statement is LockStatementSyntax
                || statement is UsingStatementSyntax
                || statement is FixedStatementSyntax;
        }

        private static bool IsConstant(ExpressionSyntax expr, SemanticModel semanticModel)
        {
            return semanticModel.GetConstantValue(expr).HasValue;
        }

        private static bool IsIntegralType(SpecialType specialType)
        {
            return specialType switch
            {
                SpecialType.System_Byte => true,
                SpecialType.System_SByte => true,
                SpecialType.System_Int16 => true,
                SpecialType.System_UInt16 => true,
                SpecialType.System_Int32 => true,
                SpecialType.System_UInt32 => true,
                SpecialType.System_Int64 => true,
                SpecialType.System_UInt64 => true,
                SpecialType.System_IntPtr => true,
                SpecialType.System_UIntPtr => true,
                _ => false,
            };
        }

        private static bool IsSpecialTypeWithConstantSupport(SpecialType specialType)
        {
            return specialType switch
            {
                SpecialType.System_Boolean => true,
                SpecialType.System_Byte => true,
                SpecialType.System_Char => true,
                SpecialType.System_Decimal => true,
                SpecialType.System_Double => true,
                SpecialType.System_Single => true,
                SpecialType.System_Int16 => true,
                SpecialType.System_Int32 => true,
                SpecialType.System_Int64 => true,
                SpecialType.System_UInt16 => true,
                SpecialType.System_UInt32 => true,
                SpecialType.System_UInt64 => true,
                SpecialType.System_SByte => true,
                SpecialType.System_String => true,
                SpecialType.System_IntPtr => true,
                SpecialType.System_UIntPtr => true,
                _ => false,
            };
        }
    }
}

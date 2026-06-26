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

            // Find the constant side: literal or cast-expression wrapping a literal
            var (constantExpr, constantText) = GetConstantExpression(binaryExpr.Left, binaryExpr.Right);
            if (constantExpr == null)
                return;

            // The other side is the non-constant expression for type checking
            var nonConstantExpr = ReferenceEquals(constantExpr, binaryExpr.Left)
                ? binaryExpr.Right
                : binaryExpr.Left;

            var typeInfo = context.SemanticModel.GetTypeInfo(nonConstantExpr);
            var type = typeInfo.Type;

            if (type == null)
                return;

            // Unwrap nullable value types
            var underlyingType = type.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T
                ? ((INamedTypeSymbol)type).TypeArguments[0]
                : type;

            bool supportsConstantPattern = underlyingType.TypeKind == TypeKind.Enum
                || IsSpecialTypeWithConstantSupport(underlyingType.SpecialType);

            if (!supportsConstantPattern)
                return;

            // For bare literals (not cast expressions), validate type compatibility.
            // Cast expressions like (char)0 are already type-safe by construction.
            if (constantExpr is LiteralExpressionSyntax bareLiteral && bareLiteral.Kind() != SyntaxKind.NullLiteralExpression)
            {
                var literalTypeInfo = context.SemanticModel.GetTypeInfo(bareLiteral);
                var literalType = literalTypeInfo.Type;
                if (literalType != null && !IsIntegralType(underlyingType.SpecialType))
                {
                    var conversion = context.Compilation.ClassifyConversion(literalType, underlyingType);
                    if (!conversion.IsImplicit && !conversion.IsIdentity)
                        return;
                }
            }

            var isEquals = binaryExpr.IsKind(SyntaxKind.EqualsExpression);
            var oldOp = isEquals ? $"== {constantText}" : $"!= {constantText}";
            var newOp = isEquals ? $"is {constantText}" : $"is not {constantText}";

            var diagnostic = Diagnostic.Create(
                Rule,
                binaryExpr.OperatorToken.GetLocation(),
                newOp,
                oldOp);

            context.ReportDiagnostic(diagnostic);
        }

        private static (ExpressionSyntax? expression, string text) GetConstantExpression(
            ExpressionSyntax left, ExpressionSyntax right)
        {
            // Direct literal: x == 0, x == null, x == true
            if (left is LiteralExpressionSyntax lit)
                return (left, lit.Token.Text);
            if (right is LiteralExpressionSyntax lit2)
                return (right, lit2.Token.Text);

            // Cast expression wrapping a literal: x == (char)0, x == (int)1
            if (left is CastExpressionSyntax castLeft && castLeft.Expression is LiteralExpressionSyntax)
                return (left, castLeft.ToString());
            if (right is CastExpressionSyntax castRight && castRight.Expression is LiteralExpressionSyntax)
                return (right, castRight.ToString());

            return (null, "");
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

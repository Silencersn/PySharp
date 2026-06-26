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

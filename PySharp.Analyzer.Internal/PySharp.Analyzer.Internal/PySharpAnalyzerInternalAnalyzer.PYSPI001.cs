using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PySharp.Analyzer.Internal;

partial class PySharpAnalyzerInternalAnalyzer
{
    /// <summary>
    /// PYSPI001 — Use pattern matching instead of equality comparison with constants.
    /// <para/>
    /// Triggers when <c>==</c> or <c>!=</c> compares against a compile-time constant
    /// (e.g., <c>null</c>, <c>true</c>, <c>false</c>, numeric literals, enum values).
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// if (x is null)
    /// if (flag is true)
    /// if (count is 0)
    /// if (day is DayOfWeek.Sunday)
    /// </code>
    /// Non-compliant:
    /// <code>
    /// if (x == null)
    /// if (flag != true)
    /// if (count == 0)
    /// if (day == DayOfWeek.Sunday)
    /// </code>
    /// Edge cases:
    /// <list type="bullet">
    ///   <item><description><c>Nullable&lt;T&gt;</c> is unwrapped automatically (e.g., <c>int? x</c> with <c>x == null</c>).</description></item>
    ///   <item><description>No diagnostic when neither side is a constant (e.g., <c>x == y</c>).</description></item>
    ///   <item><description>Incompatible literal types (e.g., string compared with int) are skipped.</description></item>
    /// </list>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSPI001 = new(
        nameof(PYSPI001),
        "Use pattern matching for constant comparisons",
        "Use '{0}' instead of '{1}'",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Use 'is' pattern matching (e.g., 'is null', 'is true', 'is 0') instead of '==' or '!=' with constants.");

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
            PYSPI001, binaryExpr.OperatorToken.GetLocation(), newOp, oldOp));
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

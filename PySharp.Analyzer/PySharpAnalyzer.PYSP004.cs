using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;

namespace PySharp.Analyzer;

partial class PySharpAnalyzer
{
    /// <summary>
    /// PYSP004 — Use a cached constant instead of an equivalent factory method call.
    /// <para/>
    /// Triggers when a <c>Py*Object</c> factory method is called with an argument that
    /// exactly matches a pre-cached static constant on the same type. The constant is
    /// bit-for-bit identical to the factory result and should be preferred for clarity
    /// and to avoid the lookup/allocation path.
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// PyIntObject.Zero
    /// PyFloatObject.One
    /// PyBoolObject.True
    /// PyStrObject.Empty
    /// </code>
    /// Non-compliant:
    /// <code>
    /// PyIntObject.FromInteger(0)
    /// PyFloatObject.FromDouble(double.NaN)
    /// PyBoolObject.FromBoolean(true)
    /// PyStrObject.FromString("")
    /// </code>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSP004 = new(
        nameof(PYSP004),
        "Use cached constant instead of factory method",
        "Use '{0}.{1}' instead of '{2}({3})'",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Prefer the cached constant on Py*Object (e.g. PyIntObject.Zero, PyBoolObject.True, PyStrObject.Empty) over an equivalent factory method call.");

    private static void AnalyzeFactoryToConstant(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Resolve the method symbol
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        // Must be a static factory method
        if (methodSymbol.MethodKind is not MethodKind.Ordinary)
            return;

        // Containing type must be a Py*Object in PySharp.Modules.Builtins
        var containingType = methodSymbol.ContainingType;
        if (containingType is null)
            return;

        if (containingType.ContainingNamespace?.ToDisplayString() is not "PySharp.Modules.Builtins")
            return;

        // Must have exactly one argument
        if (invocation.ArgumentList.Arguments.Count is not 1)
            return;

        var argument = invocation.ArgumentList.Arguments[0].Expression;

        // Dispatch to the matcher for the (type, method) pair
        string? constant = (containingType.Name, methodSymbol.Name) switch
        {
            ("PyIntObject", "FromInteger") => MatchIntConstant(argument, context),
            ("PyFloatObject", "FromDouble") => MatchFloatConstant(argument, context),
            ("PyBoolObject", "FromBoolean") => MatchBoolConstant(argument, context),
            ("PyStrObject", "FromString") => MatchStrConstant(argument, context),
            _ => null
        };

        if (constant is null)
            return;

        // Self-reference guard: skip when the call initializes the very constant being
        // suggested (e.g. `PyFloatObject.Zero { get; } = FromDouble(0);`).
        // Uses the syntax tree rather than GetEnclosingSymbol, which does not reliably
        // return the property symbol for auto-property initializers.
        var enclosingProperty = invocation.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        if (enclosingProperty is not null && enclosingProperty.Identifier.ValueText == constant)
            return;

        // Report diagnostic at the invocation
        context.ReportDiagnostic(Diagnostic.Create(
            PYSP004, invocation.GetLocation(),
            containingType.Name, constant, methodSymbol.Name, argument.ToString()));
    }

    /// <summary>Matches <c>FromInteger(0|1|-1)</c> and <c>FromInteger(BigInteger.Zero|One|MinusOne)</c>.</summary>
    private static string? MatchIntConstant(ExpressionSyntax argument, SyntaxNodeAnalysisContext context)
    {
        if (IsLiteralOrSignedLiteral(argument))
        {
            var constValue = context.SemanticModel.GetConstantValue(argument, context.CancellationToken);
            if (constValue.HasValue)
            {
                long? value = GetConstantIntegerValue(constValue);
                if (value is 0 or 1 or -1)
                    return value switch { 0 => "Zero", 1 => "One", _ => "MinusOne" };
            }
        }

        return MatchNamedMember(argument, context,
            new[] { "Zero", "One", "MinusOne" }, "BigInteger", "System.Numerics");
    }

    /// <summary>Matches <c>FromDouble</c> for bit-identical literal values and <c>double.XXX</c> constants.</summary>
    private static string? MatchFloatConstant(ExpressionSyntax argument, SyntaxNodeAnalysisContext context)
    {
        if (IsLiteralOrSignedLiteral(argument))
        {
            var constValue = context.SemanticModel.GetConstantValue(argument, context.CancellationToken);
            if (constValue.HasValue)
            {
                // -0.0 as double is not available on netstandard2.0, construct it bit-wise
                double negativeZero = BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000));

                if (BitMatchesDouble(constValue, 0.0)) return "Zero";
                if (BitMatchesDouble(constValue, negativeZero)) return "NegativeZero";
                if (BitMatchesDouble(constValue, 1.0)) return "One";
                if (BitMatchesDouble(constValue, -1.0)) return "MinusOne";
            }
        }

        return MatchNamedMember(argument, context,
            new[] { "NegativeZero", "NaN", "PositiveInfinity", "NegativeInfinity", "Pi", "E", "Epsilon", "Tau" },
            "Double", "System");
    }

    /// <summary>Matches <c>FromBoolean(true|false)</c> literals only.</summary>
    private static string? MatchBoolConstant(ExpressionSyntax argument, SyntaxNodeAnalysisContext context)
    {
        if (!IsLiteralOrSignedLiteral(argument))
            return null;

        var constValue = context.SemanticModel.GetConstantValue(argument, context.CancellationToken);
        if (!constValue.HasValue || constValue.Value is not bool value)
            return null;

        return value ? "True" : "False";
    }

    /// <summary>Matches <c>FromString("")</c> and <c>FromString(string.Empty)</c>.</summary>
    private static string? MatchStrConstant(ExpressionSyntax argument, SyntaxNodeAnalysisContext context)
    {
        if (IsLiteralOrSignedLiteral(argument))
        {
            var constValue = context.SemanticModel.GetConstantValue(argument, context.CancellationToken);
            if (constValue.HasValue && constValue.Value is string text && text.Length is 0)
                return "Empty";
        }

        return MatchNamedMember(argument, context, new[] { "Empty" }, "String", "System");
    }

    /// <summary>
    /// Matches a member access like <c>BigInteger.Zero</c> / <c>double.NaN</c> / <c>string.Empty</c>
    /// where the member is a static field or property of the given containing type.
    /// </summary>
    private static string? MatchNamedMember(ExpressionSyntax argument, SyntaxNodeAnalysisContext context,
        string[] names, string metadataTypeName, string ns)
    {
        if (argument is not MemberAccessExpressionSyntax memberAccess)
            return null;

        var memberName = memberAccess.Name.Identifier.ValueText;
        if (System.Array.IndexOf(names, memberName) < 0)
            return null;

        var symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        if (symbol is null)
            return null;

        if (symbol is IFieldSymbol or IPropertySymbol && HasContainingType(symbol, metadataTypeName, ns))
            return memberName;

        return null;
    }

    /// <summary>
    /// Checks the containing type by metadata name and namespace. Avoids ToDisplayString,
    /// which renders System.Double/System.String as keywords ("double"/"string") and may
    /// add a "global::" prefix depending on the format.
    /// </summary>
    private static bool HasContainingType(ISymbol symbol, string metadataName, string ns)
    {
        var containingType = symbol.ContainingType;
        if (containingType is null)
            return false;

        return containingType.MetadataName == metadataName &&
               containingType.ContainingNamespace?.ToDisplayString() == ns;
    }

    /// <summary>
    /// True when the expression is a literal, or a unary +/- applied to a literal
    /// (e.g. <c>0</c>, <c>0L</c>, <c>true</c>, <c>""</c>, <c>-1</c>, <c>-0.0</c>).
    /// Constant fields referenced by name or member access are intentionally excluded:
    /// they carry intent and may be retargeted, so only literals are folded.
    /// </summary>
    private static bool IsLiteralOrSignedLiteral(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax)
            return true;

        return expression is PrefixUnaryExpressionSyntax unary &&
               (unary.RawKind == (int)SyntaxKind.UnaryMinusExpression ||
                unary.RawKind == (int)SyntaxKind.UnaryPlusExpression) &&
               unary.Operand is LiteralExpressionSyntax;
    }

    /// <summary>Extracts a small integer constant value (0, 1, -1 range is sufficient for callers).</summary>
    private static long? GetConstantIntegerValue(Optional<object?> constValue)
    {
        if (!constValue.HasValue || constValue.Value is null)
            return null;

        return constValue.Value switch
        {
            int i => i,
            long l => l,
            uint u => u,
            ulong ul when ul <= long.MaxValue => (long)ul,
            _ => null
        };
    }

    /// <summary>Bit-exact comparison of a constant value with a target double (distinguishes ±0).</summary>
    private static bool BitMatchesDouble(Optional<object?> constValue, double target)
    {
        if (!constValue.HasValue || constValue.Value is null)
            return false;

        double? value = constValue.Value switch
        {
            double dv => dv,
            float f => f,
            int i => i,
            long l => l,
            uint u => u,
            ulong ul => ul,
            _ => null
        };

        if (value is null)
            return false;

        return BitConverter.DoubleToInt64Bits(value.Value) == BitConverter.DoubleToInt64Bits(target);
    }
}

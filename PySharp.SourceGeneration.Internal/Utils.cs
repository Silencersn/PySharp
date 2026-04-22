using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PySharp.SourceGeneration.Internal;

internal class Utils
{
    public static IncrementalValuesProvider<INamedTypeSymbol> CreateNamedTypeSymbolProvider(IncrementalGeneratorInitializationContext context, string fullyQualifiedName)
    {
        return context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is BaseTypeDeclarationSyntax,
            transform: (ctx, _) =>
            {
                if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is INamedTypeSymbol symbol &&
                    symbol.ToDisplayString() == fullyQualifiedName)
                {
                    return symbol;
                }

                return null;
            }
        ).Where(static x => x is not null)!;
    }
}

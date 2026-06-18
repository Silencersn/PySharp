using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PySharp.SourceGeneration.Internal;

[Generator]
public partial class InternalPyTypeObjectGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var declarationsProvider = Utils.CreateNamedTypeSymbolProvider(context, "PySharp.Modules.Builtins.PyTypeObject.Declarations");

        context.RegisterSourceOutput(declarationsProvider, static (ctx, symbol) =>
        {
            var methods = symbol.GetMembers().OfType<IMethodSymbol>().Where(m => m.IsPartialDefinition);

            var methodsList = methods.ToList();

            // Pre-compute SlotsMember info for each method
            var methodSlotsMember = methodsList.ToDictionary(
                m => m,
                m => GetSlotsMemberName(m), SymbolEqualityComparer.Default);

            // Find PyTypeSlots to resolve field types for SlotsMember groups
            var pyTypeSlotsSymbol = symbol.ContainingType.GetTypeMembers("PyTypeSlots").FirstOrDefault();

            // Group methods by SlotsMember
            var directMethods = methodsList.Where(m => methodSlotsMember[m] == null).ToList();
            var slotsMemberGroups = methodsList
                .Where(m => methodSlotsMember[m] != null)
                .GroupBy(m => methodSlotsMember[m]!)
                .ToList();

            // Resolve field and its type for each SlotsMember value
            var slotsMemberFieldTypes = new Dictionary<string, (IFieldSymbol field, INamedTypeSymbol type)>(StringComparer.Ordinal);
            foreach (var group in slotsMemberGroups)
            {
                var fieldName = group.Key;
                var field = pyTypeSlotsSymbol?.GetMembers(fieldName).OfType<IFieldSymbol>().FirstOrDefault();
                if (field?.Type is INamedTypeSymbol namedType)
                    slotsMemberFieldTypes[fieldName] = (field, namedType);
            }

            GenerateVirtualFile(ctx, methodsList);
            GenerateSealedFile(ctx, methodsList);
            GenerateSlotsFile(ctx, directMethods, slotsMemberGroups, slotsMemberFieldTypes);
            GenerateSpecialNamesFile(ctx, methodsList);
            GeneratePartialDeclarationsFile(ctx, methodsList);

        });
    }

    private static string? GetSlotsMemberName(IMethodSymbol method)
    {
        var attr = method.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "PySpecialMethodAttribute");
        if (attr == null) return null;
        foreach (var kvp in attr.NamedArguments)
            if (kvp.Key == "SlotsMember" && kvp.Value.Value is string sm)
                return sm;
        return null;
    }

    private static string GetExtensionMethodName(string delegateName)
        => delegateName.StartsWith("Py") ? "To" + delegateName.Substring(2) : "To" + delegateName;
}

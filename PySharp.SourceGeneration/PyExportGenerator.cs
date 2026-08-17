using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PySharp.SourceGeneration.Diagnostics;
using PySharp.SourceGeneration.Utility;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PySharp.SourceGeneration;

/// <summary>
/// Generates the implementation of properties annotated with <c>[PyExport]</c>. It reads the
/// <c>[PyFunctionParameters]</c> attribute from every referenced implementation method at compile
/// time, so the generated code carries explicit parameter definitions and no runtime reflection is
/// needed when constructing the exported built-in function object.
/// </summary>
[Generator]
public class PyExportGenerator : IIncrementalGenerator
{
    private const string PyFunctionParametersAttribute = "PySharp.Runtime.PyAttributes.PyFunctionParametersAttribute";
    private const string PyResultType = "PySharp.Runtime.Calls.PyResult";
    private const string PyCallContextType = "PySharp.Runtime.Calls.PyCallContext";
    private const string PyArgumentsType = "PySharp.Runtime.Calls.PyArguments";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: PySharpTypes.PyExportAttribute,
                predicate: static (syntaxNode, _) => syntaxNode is PropertyDeclarationSyntax,
                transform: static (generatorContext, _) =>
                {
                    var property = (IPropertySymbol)generatorContext.TargetSymbol;
                    var attribute = generatorContext.Attributes[0];
                    return Decode(property, attribute);
                })
            .WhereNotNull()
            .Collect();

        context.RegisterSourceOutput(provider, static (context, items) =>
        {
            var validExports = new List<ExportInfo>();
            foreach (var item in items)
            {
                if (item.HasDiagnostic)
                    context.ReportAll(item.Diagnostics);
                else
                    validExports.Add(item.Value);
            }

            if (validExports.Count is 0)
                return;

            foreach (var group in validExports.GroupBy(static export => (export.Namespace, export.TypeName)))
                GenerateSource(context, group.Key.Namespace, group.Key.TypeName, group.ToList());
        });
    }

    private static DiagnosticOr<ExportInfo>? Decode(IPropertySymbol property, AttributeData attribute)
    {
        var args = attribute.ConstructorArguments;
        if (args.Length is 0 || args[0].Kind == TypedConstantKind.Error || args[0].Value is not string exportedName)
            return null; // Undecodable name argument: skip silently.

        if (string.IsNullOrEmpty(exportedName))
            return DiagnosticOr<ExportInfo>.From(DiagnosticInfo.For(attribute, PyGeneratorDiagnostics.ExportNameNullOrEmpty, property.Name));

        if (args.Length < 2 || args[1].Kind == TypedConstantKind.Error)
            return null; // Undecodable params array: skip silently.

        var containingType = property.ContainingType;
        var methods = ImmutableArray.CreateBuilder<ExportMethodInfo>();
        foreach (var methodConstant in args[1].Values)
        {
            if (methodConstant.Kind == TypedConstantKind.Error || methodConstant.Value is not string methodName)
                return null; // Undecodable element: skip silently.

            var method = containingType.GetMembers(methodName).OfType<IMethodSymbol>().FirstOrDefault(static m => !m.IsImplicitlyDeclared);
            if (method is null)
                return DiagnosticOr<ExportInfo>.From(DiagnosticInfo.For(
                    attribute, PyGeneratorDiagnostics.ExportMethodNotFound,
                    methodName, property.Name, containingType.ToDisplayString()));

            var parametersAttribute = method.GetAttribute(PyFunctionParametersAttribute);
            if (parametersAttribute is null)
                return DiagnosticOr<ExportInfo>.From(DiagnosticInfo.For(
                    attribute, PyGeneratorDiagnostics.ExportMethodMissingParameters,
                    methodName, property.Name));

            if (!IsPyFunctionCompatible(method))
                return DiagnosticOr<ExportInfo>.From(DiagnosticInfo.For(
                    attribute, PyGeneratorDiagnostics.ExportMethodSignatureIncompatible,
                    methodName, property.Name));

            var parameterStrings = ReadParameters(parametersAttribute);
            if (parameterStrings is not { } parameters)
                return null; // Undecodable parameters: skip silently.

            methods.Add(new ExportMethodInfo(methodName, parameters));
        }

        return DiagnosticOr<ExportInfo>.From(new ExportInfo(
            property.ContainingNamespace.ToDisplayString(),
            containingType.Name,
            property.Name,
            property.Type.ToDisplayString(),
            exportedName,
            methods.ToImmutable()));
    }

    private static bool IsPyFunctionCompatible(IMethodSymbol method)
    {
        if (method.ReturnsVoid || method.ReturnType.ToDisplayString() != PyResultType)
            return false;
        if (method.Parameters.Length != 2)
            return false;
        return method.Parameters[0].Type.ToDisplayString() == PyCallContextType
            && method.Parameters[1].Type.ToDisplayString() == PyArgumentsType;
    }

    private static ImmutableArray<string>? ReadParameters(AttributeData parametersAttribute)
    {
        var args = parametersAttribute.ConstructorArguments;
        if (args.Length is 0)
            return [];

        if (args[0].Kind == TypedConstantKind.Error)
            return null;

        var builder = ImmutableArray.CreateBuilder<string>(args[0].Values.Length);
        foreach (var constant in args[0].Values)
        {
            if (constant.Kind == TypedConstantKind.Error || constant.Value is not string value)
                return null;
            builder.Add(value);
        }
        return builder.ToImmutable();
    }

    private static void GenerateSource(SourceProductionContext spc, string namespaceName, string className, List<ExportInfo> exports)
    {
        var builder = new IndentedStringBuilder();
        builder
            .AppendAutoGeneratedTag()
            .EnableNullable()
            .UsingNamespace("PySharp.Runtime.Calls")
            .UsingNamespace("PySharp.Modules.Builtins")
            .AppendLine();

        if (!string.IsNullOrEmpty(namespaceName))
        {
            builder
                .AppendLine($"namespace {namespaceName}")
                .EnterBlock();
        }

        builder
            .AppendLine($"partial class {className}")
            .EnterBlock();

        foreach (var export in exports)
        {
            var fieldName = $"s__py_impl_field_{export.PropertyName}";
            builder.AppendLine($"private static readonly {export.PropertyType} {fieldName} = PyBuiltinFunctionOrMethodObject.CreateFunction(");
            builder.Indent();
            builder.AppendLine($"{FormatLiteral(export.ExportedName)},");
            for (int i = 0; i < export.Methods.Length; i++)
            {
                var method = export.Methods[i];
                var separator = i < export.Methods.Length - 1 ? "," : string.Empty;
                builder.Append($"new PyDelegateDefinition<PyFunction>({method.Name}, [");
                builder.Append(string.Join(", ", method.Parameters.Select(FormatLiteral)));
                builder.AppendLine($"]){separator}");
            }
            builder.Dedent();
            builder.AppendLine(");");
            builder.AppendLine();
            builder.AppendLine($"public static partial {export.PropertyType} {export.PropertyName} => {fieldName};");
            builder.AppendLine();
        }

        builder
            .ExitBlock(); // class

        if (!string.IsNullOrEmpty(namespaceName))
            builder.ExitBlock(); // namespace

        spc.AddSource($"{className}.PyExport.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private static string FormatLiteral(string value)
    {
        return SymbolDisplay.FormatLiteral(value, quote: true);
    }

    private sealed record ExportInfo
    {
        public ExportInfo(string @namespace, string typeName, string propertyName, string propertyType, string exportedName, ImmutableArray<ExportMethodInfo> methods)
        {
            Namespace = @namespace;
            TypeName = typeName;
            PropertyName = propertyName;
            PropertyType = propertyType;
            ExportedName = exportedName;
            Methods = methods;
        }

        public string Namespace { get; }
        public string TypeName { get; }
        public string PropertyName { get; }
        public string PropertyType { get; }
        public string ExportedName { get; }
        public ImmutableArray<ExportMethodInfo> Methods { get; }
    }

    private sealed record ExportMethodInfo
    {
        public ExportMethodInfo(string name, ImmutableArray<string> parameters)
        {
            Name = name;
            Parameters = parameters;
        }

        public string Name { get; }
        public ImmutableArray<string> Parameters { get; }
    }
}

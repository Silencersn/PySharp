using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PySharp.SourceGeneration.Diagnostics;
using PySharp.SourceGeneration.Utility;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace PySharp.SourceGeneration;

[Generator]
public class PyModuleIncludeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: PySharpTypes.PyModuleIncludeAttribute,
                predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
                transform: static (generatorContext, _) =>
                {
                    var typeSymbol = (INamedTypeSymbol)generatorContext.TargetSymbol;
                    var className = typeSymbol.Name;
                    var ns = typeSymbol.ContainingNamespace.ToDisplayString();

                    var results = new List<DiagnosticOr<ModuleIncludeAttrInfo>>();
                    foreach (var attr in generatorContext.Attributes)
                    {
                        var result = ExtractIncludeInfo(attr);
                        if (result is not null)
                            results.Add(result);
                    }
                    return new ClassIncludes(className, ns, results);
                });

        context.RegisterSourceOutput(provider, static (context, classIncludes) =>
        {
            var validInfos = new List<ModuleIncludeAttrInfo>();
            foreach (var include in classIncludes.Infos)
            {
                if (include.HasDiagnostic)
                    context.ReportAll(include.Diagnostics);
                else
                    validInfos.Add(include.Value);
            }

            if (validInfos.Count is 0)
                return;

            GenerateSource(context, classIncludes.Namespace, classIncludes.ClassName, validInfos);
        });
    }

    private static DiagnosticOr<ModuleIncludeAttrInfo>? ExtractIncludeInfo(AttributeData attr)
    {
        var schemeArgs = attr.ConstructorArguments;
        Debug.Assert(schemeArgs.Length > 0);
        if (schemeArgs[0].Kind == TypedConstantKind.Error || schemeArgs[0].Value is null)
            return null; // Undecodable constant: skip silently.

        var schemeOrdinal = (int)schemeArgs[0].Value!;
        if (schemeOrdinal < 0 || schemeOrdinal > 2)
            return DiagnosticOr<ModuleIncludeAttrInfo>.From(DiagnosticInfo.InvalidEnumValue(attr, "scheme", schemeOrdinal, "PyModuleIncludeScheme"));

        switch (schemeOrdinal)
        {
            case 0: // StaticMembers(Type sourceType)
                {
                    if (!attr.TryGetTypeArgument(1, "sourceType", out var sourceType, out var sourceTypeError))
                        return sourceTypeError?.ToDiagnosticOr<ModuleIncludeAttrInfo>();

                    var sourceTypeFullName = sourceType.ToDisplayString();
                    var members = GetExportableStaticMembers(sourceType);
                    return DiagnosticOr<ModuleIncludeAttrInfo>.From(new ModuleIncludeAttrInfo(sourceTypeFullName, null, members));
                }

            case 1: // TypeSingleton(Type sourceType)
                {
                    if (!attr.TryGetTypeArgument(1, "sourceType", out var sourceType, out var sourceTypeError))
                        return sourceTypeError?.ToDiagnosticOr<ModuleIncludeAttrInfo>();

                    var sourceTypeFullName = sourceType.ToDisplayString();
                    return DiagnosticOr<ModuleIncludeAttrInfo>.From(new ModuleIncludeAttrInfo(sourceTypeFullName, null,
                        [new MemberInfo("Shared", sourceTypeFullName, true)]));
                }

            case 2: // ExplicitMember(string name, Type sourceType, string memberName)
                {
                    var errors = ImmutableArray.CreateBuilder<DiagnosticInfo>();
                    if (!attr.TryGetRequiredStringArgument(1, "name", out var name, out var nameError))
                    {
                        if (nameError is not null)
                            errors.Add(nameError);
                        else
                            return null; // Undecodable: skip silently.
                    }

                    if (!attr.TryGetTypeArgument(2, "sourceType", out var sourceType, out var sourceTypeError))
                    {
                        if (sourceTypeError is not null)
                            errors.Add(sourceTypeError);
                        else
                            return null; // Undecodable: skip silently.
                    }

                    if (!attr.TryGetRequiredStringArgument(3, "memberName", out var memberName, out var memberNameError))
                    {
                        if (memberNameError is not null)
                            errors.Add(memberNameError);
                        else
                            return null; // Undecodable: skip silently.
                    }

                    if (errors.Count > 0)
                        return DiagnosticOr<ModuleIncludeAttrInfo>.From(errors);

                    // Only reached when all three arguments decoded successfully.
                    var sourceTypeFullName = sourceType!.ToDisplayString();
                    return DiagnosticOr<ModuleIncludeAttrInfo>.From(new ModuleIncludeAttrInfo(sourceTypeFullName, name!,
                        [new MemberInfo(memberName!, sourceTypeFullName, false)]));
                }

            default:
                return null;
        }
    }

    private static List<MemberInfo> GetExportableStaticMembers(ITypeSymbol sourceType)
    {
        var members = new List<MemberInfo>();

        foreach (var member in sourceType.GetMembers())
        {
            ITypeSymbol memberType;
            string memberName;

            if (member is IFieldSymbol field)
            {
                if (!field.IsStatic || field.DeclaredAccessibility is not Accessibility.Public)
                    continue;
                memberType = field.Type;
                memberName = field.Name;
            }
            else if (member is IPropertySymbol prop)
            {
                if (!prop.IsStatic || prop.DeclaredAccessibility is not Accessibility.Public)
                    continue;
                memberType = prop.Type;
                memberName = prop.Name;
            }
            else
            {
                continue;
            }

            if (!IsPyObject(memberType))
                continue;

            bool hasName = HasIPyObjectName(memberType);
            members.Add(new MemberInfo(memberName, sourceType.ToDisplayString(), hasName));
        }

        return members;
    }

    private static bool IsPyObject(ITypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            if (current.Name is "PyObject" && current.ContainingNamespace?.ToDisplayString() is "PySharp.Modules.Builtins")
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static bool HasIPyObjectName(ITypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.Name is "IPyObjectName" && iface.ContainingNamespace?.ToDisplayString() is "PySharp.Modules")
                return true;
        }
        return false;
    }

    private static void GenerateSource(SourceProductionContext spc, string namespaceName, string className, List<ModuleIncludeAttrInfo> includes)
    {
        var builder = new IndentedStringBuilder();
        builder
            .AppendAutoGeneratedTag()
            .EnableNullable()
            .UsingNamespace("PySharp.Runtime.PyAttributes")
            .AppendLine();

        if (!string.IsNullOrEmpty(namespaceName))
        {
            builder
                .AppendLine($"namespace {namespaceName}")
                .EnterBlock();
        }

        builder
            .AppendLine($"partial class {className}")
            .EnterBlock()
            .AppendLine("protected override void ApplyIncludes()")
            .EnterBlock();

        foreach (var include in includes)
        {
            if (include.Members.Count is 0)
                continue;

            builder.AppendLineComment($"// {include.SourceTypeFullName}");

            foreach (var member in include.Members)
            {
                if (include.ExplicitName is not null)
                {
                    // ExplicitMember: AppendAttribute("name", Type.Member)
                    builder.AppendLine($"AppendAttribute(\"{include.ExplicitName}\", {member.SourceTypeFullName}.{member.Name});");
                }
                else if (member.HasName)
                {
                    // IPyObjectName overload: AppendAttribute(Type.Member)
                    builder.AppendLine($"AppendAttribute({member.SourceTypeFullName}.{member.Name});");
                }
                else
                {
                    // Explicit name fallback: use member name lowercase
                    builder.AppendLine($"AppendAttribute(\"{member.Name.ToLowerInvariant()}\", {member.SourceTypeFullName}.{member.Name});");
                }
            }

            builder.AppendLine();
        }

        builder
            .ExitBlock() // ApplyIncludes
            .ExitBlock(); // class

        if (!string.IsNullOrEmpty(namespaceName))
            builder.ExitBlock(); // namespace

        spc.AddSource($"{className}.PyModuleInclude.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private sealed class ModuleIncludeAttrInfo
    {
        public ModuleIncludeAttrInfo(string sourceTypeFullName, string? explicitName, List<MemberInfo> members)
        {
            SourceTypeFullName = sourceTypeFullName;
            ExplicitName = explicitName;
            Members = members;
        }

        public string SourceTypeFullName { get; }
        public string? ExplicitName { get; }
        public List<MemberInfo> Members { get; }
    }

    private sealed class MemberInfo
    {
        public MemberInfo(string name, string sourceTypeFullName, bool hasName)
        {
            Name = name;
            SourceTypeFullName = sourceTypeFullName;
            HasName = hasName;
        }

        public string Name { get; }
        public string SourceTypeFullName { get; }
        public bool HasName { get; }
    }

    private sealed class ClassIncludes
    {
        public ClassIncludes(string className, string @namespace, List<DiagnosticOr<ModuleIncludeAttrInfo>> infos)
        {
            ClassName = className;
            Namespace = @namespace;
            Infos = infos;
        }

        public string ClassName { get; }
        public string Namespace { get; }
        public List<DiagnosticOr<ModuleIncludeAttrInfo>> Infos { get; }
    }
}

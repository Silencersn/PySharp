using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PySharp.SourceGeneration.Utility;
using System.Collections.Generic;
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

                    var results = new List<ModuleIncludeAttrInfo>();
                    foreach (var attr in generatorContext.Attributes)
                    {
                        var info = ExtractIncludeInfo(attr);
                        if (info is not null)
                            results.Add(info);
                    }
                    return new ClassIncludes(className, ns, results);
                });

        context.RegisterSourceOutput(provider, static (spc, classIncludes) =>
        {
            if (classIncludes.Infos.Count is 0)
                return;

            GenerateSource(spc, classIncludes.Namespace, classIncludes.ClassName, classIncludes.Infos);
        });
    }

    private static ModuleIncludeAttrInfo? ExtractIncludeInfo(AttributeData attr)
    {
        var schemeOrdinal = attr.GetConstructorArgument(0, -1);
        if (schemeOrdinal < 0 || schemeOrdinal > 2)
            return null;

        switch (schemeOrdinal)
        {
            case 0: // StaticMembers(Type sourceType)
                {
                    var sourceType = attr.GetConstructorArgument<ITypeSymbol>(1);
                    if (sourceType is null)
                        return null;
                    var sourceTypeFullName = sourceType.ToDisplayString();
                    var members = GetExportableStaticMembers(sourceType);
                    return new ModuleIncludeAttrInfo(sourceTypeFullName, null, members);
                }

            case 1: // TypeSingleton(Type sourceType)
                {
                    var sourceType = attr.GetConstructorArgument<ITypeSymbol>(1);
                    if (sourceType is null)
                        return null;
                    var sourceTypeFullName = sourceType.ToDisplayString();
                    return new ModuleIncludeAttrInfo(sourceTypeFullName, null,
                        [new MemberInfo("Shared", sourceTypeFullName, true)]);
                }

            case 2: // ExplicitMember(string name, Type sourceType, string memberName)
                {
                    var name = attr.GetConstructorArgument<string>(1);
                    var sourceType = attr.GetConstructorArgument<ITypeSymbol>(2);
                    var memberName = attr.GetConstructorArgument<string>(3);
                    if (sourceType is null || name is null || memberName is null)
                        return null;
                    var sourceTypeFullName = sourceType.ToDisplayString();
                    return new ModuleIncludeAttrInfo(sourceTypeFullName, name,
                        [new MemberInfo(memberName, sourceTypeFullName, false)]);
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
        public ClassIncludes(string className, string @namespace, List<ModuleIncludeAttrInfo> infos)
        {
            ClassName = className;
            Namespace = @namespace;
            Infos = infos;
        }

        public string ClassName { get; }
        public string Namespace { get; }
        public List<ModuleIncludeAttrInfo> Infos { get; }
    }
}

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PySharp.SourceGeneration.Diagnostics;
using PySharp.SourceGeneration.Utility;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace PySharp.SourceGeneration;

[Generator]
public class PyFrozenModuleGenerator : IIncrementalGenerator
{
    private const string PyFrozenModuleObjectType = "PySharp.Modules.Builtins.PyFrozenModuleObject";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pipeline A: collect all classes with [PyFrozenModule] attribute
        var frozenModules = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: PySharpTypes.PyFrozenModuleAttribute,
                predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
                transform: static (generatorContext, _) =>
                {
                    var typeSymbol = (INamedTypeSymbol)generatorContext.TargetSymbol;

                    // Verify the class inherits from PyFrozenModuleObject
                    if (!InheritsFromFrozenModule(typeSymbol))
                        return default;

                    var className = typeSymbol.Name;
                    var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
                        ? string.Empty
                        : typeSymbol.ContainingNamespace.ToDisplayString();
                    var attribute = generatorContext.Attributes[0];

                    // Both required arguments must be decodable; an explicitly null value becomes a
                    // diagnostic, an undecodable constant is a silent skip (null).
                    var errors = ImmutableArray.CreateBuilder<DiagnosticInfo>();
                    if (!attribute.TryGetRequiredStringArgument(0, "moduleName", out var moduleName, out var moduleNameError))
                    {
                        if (moduleNameError is not null)
                            errors.Add(moduleNameError);
                        else
                            return null; // Undecodable: skip silently.
                    }

                    if (!attribute.TryGetRequiredStringArgument(1, "pythonFilePath", out var pythonFilePath, out var pythonFilePathError))
                    {
                        if (pythonFilePathError is not null)
                            errors.Add(pythonFilePathError);
                        else
                            return null; // Undecodable: skip silently.
                    }
                    
                    if (errors.Count > 0)
                        return DiagnosticOr<FrozenModuleInfo>.From(errors);

                    // Only reached when both arguments decoded successfully, so the values are non-null.
                    return DiagnosticOr<FrozenModuleInfo>.From(new FrozenModuleInfo(className, ns, moduleName!, pythonFilePath!));
                })
            .WhereNotNull();

        // Pipeline B: collect all AdditionalFiles that end with .py
        var pyFiles = context.AdditionalTextsProvider
            .Where(static at => at.Path.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            .Select(static (at, ct) => (at, at.GetText(ct)?.ToString()))
            .Where(static tuple => tuple.Item2 is not null)
            .Select(static (tuple, ct) => new PyFileInfo(tuple.at.Path, tuple.Item2!))
            .Collect();

        // Combine pipelines: for each frozen module, find the matching .py AdditionalFile
        // by matching the relative path suffix against the full AdditionalText path.
        var combined = frozenModules.Collect().Combine(pyFiles);

        context.RegisterSourceOutput(combined, static (context, tuple) =>
        {
            var (modules, files) = tuple;
            if (modules.Length is 0)
                return;

            foreach (var module in modules)
            {
                // Report any argument errors and skip generating the broken module.
                if (module.HasDiagnostic)
                {
                    context.ReportAll(module.Diagnostics);
                    continue;
                }

                var info = module.Value;

                // Normalize the expected relative path (e.g. "Lib/this.py")
                var expectedSuffix = NormalizePath(info.PythonFilePath);

                // Match by checking if the AdditionalFile's full path ends with the
                // expected relative path (case-insensitive, normalized slashes).
                var match = files.FirstOrDefault(f =>
                {
                    var filePath = NormalizePath(f.FullPath);
                    return filePath.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase)
                        // Ensure the match is for the full last path segment, not a substring
                        && (filePath.Length == expectedSuffix.Length
                            || filePath[filePath.Length - expectedSuffix.Length - 1] == '/');
                });

                if (match is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        PyGeneratorDiagnostics.PyFileNotFound,
                        Location.None,
                        info.PythonFilePath,
                        info.ClassName,
                        string.Join("; ", files.Select(f => f.FullPath))));
                    continue;
                }

                GenerateSource(context, info, match.Content);
            }
        });
    }

    private static bool InheritsFromFrozenModule(INamedTypeSymbol typeSymbol)
    {
        var current = typeSymbol.BaseType;
        while (current is not null)
        {
            if (current.ToDisplayString() == PyFrozenModuleObjectType)
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static void GenerateSource(SourceProductionContext spc, FrozenModuleInfo info, string pythonCode)
    {
        var builder = new IndentedStringBuilder();
        builder
            .AppendAutoGeneratedTag()
            .EnableNullable()
            .AppendLine();

        if (!string.IsNullOrEmpty(info.Namespace))
        {
            builder
                .AppendLine($"namespace {info.Namespace}")
                .EnterBlock();
        }

        builder
            .AppendLine($"partial class {info.ClassName}")
            .EnterBlock()
            .AppendLine($"public {info.ClassName}() : base(\"{info.ModuleName}\")")
            .EnterBlock()
            .ExitBlock()
            .AppendLine()
            .Append("public override string Code => ")
            .Append(RawStringLiteral(pythonCode))
            .AppendLine(";")
            .ExitBlock(); // class

        if (!string.IsNullOrEmpty(info.Namespace))
            builder.ExitBlock(); // namespace

        spc.AddSource($"{info.ClassName}.PyFrozenModule.g.cs",
            SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    /// <summary>
    /// Formats <paramref name="text"/> as a C# raw string literal (<c>"""..."""</c>),
    /// automatically determining the minimum required quote count to avoid conflicts.
    /// </summary>
    private static string RawStringLiteral(string text)
    {
        // Count the max consecutive '"' characters in the text
        int maxQuotes = 0;
        int currentRun = 0;
        foreach (var c in text)
        {
            if (c == '"')
            {
                currentRun++;
                if (currentRun > maxQuotes)
                    maxQuotes = currentRun;
            }
            else
            {
                currentRun = 0;
            }
        }

        // Minimum delimiter length is 3; must exceed the longest quote run
        int delimiterLength = Math.Max(3, maxQuotes + 1);
        string delimiter = new string('"', delimiterLength);

        // For raw string literals, the opening and closing delimiters must be on their own lines
        // when the content spans multiple lines (or contains quotes).
        // If the content is a single line without quotes, we can inline it.
        bool hasNewline = text.Contains('\n') || text.Contains('\r');
        bool hasQuotes = maxQuotes > 0;

        if (!hasNewline && !hasQuotes)
        {
            return $"{delimiter}{text}{delimiter}";
        }

        // Multi-line raw string literal: opening/closing delimiters on separate lines
        return $"{delimiter}\r\n{text}\r\n{delimiter}";
    }

    private static string NormalizePath(string path)
    {
        // Normalize to forward slashes and remove trailing separator
        return path.Replace('\\', '/').TrimEnd('/');
    }

    private sealed class FrozenModuleInfo
    {
        public FrozenModuleInfo(string className, string @namespace, string moduleName, string pythonFilePath)
        {
            ClassName = className;
            Namespace = @namespace;
            ModuleName = moduleName;
            PythonFilePath = pythonFilePath;
        }

        public string ClassName { get; }
        public string Namespace { get; }
        public string ModuleName { get; }
        public string PythonFilePath { get; }
    }

    private sealed class PyFileInfo
    {
        public PyFileInfo(string fullPath, string content)
        {
            FullPath = fullPath;
            Content = content;
        }

        public string FullPath { get; }
        public string Content { get; }
    }
}

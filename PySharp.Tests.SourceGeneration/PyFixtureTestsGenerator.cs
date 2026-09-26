using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PySharp.Tests.SourceGeneration;

/// <summary>
/// Scans the Python fixture corpus (test_pyfiles/*.py, fed as AdditionalFiles)
/// and emits one MSTest [TestMethod] per fixture whose docstring metadata
/// declares :kind: test. Fixtures marked :kind: helper are imported by other
/// fixtures and are skipped.
///
/// The docstring is also the enforcement point for the corpus conventions
/// (docs/reference/contributing/test-corpus.md): a missing or unterminated
/// docstring, a missing or invalid :kind: field, or a file name containing
/// "regression" fails the build; unknown fields and "Regression:" title
/// lines warn.
/// </summary>
[Generator]
public sealed class PyFixtureTestsGenerator : IIncrementalGenerator
{
    private const string RunnerType = "PyFixtureRunner";
    private const string TestClassName = "PyFileTests";
    private const string TestNamespace = "PySharp.Tests";

    private static readonly DiagnosticDescriptor MissingModuleDocstring = new(
        id: "PYFIX001",
        title: "Fixture has no module docstring",
        messageFormat: "Fixture '{0}' must start with a module docstring containing a ':kind:' field (docs/reference/contributing/test-corpus.md)",
        category: "PySharp.Tests.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnterminatedDocstring = new(
        id: "PYFIX002",
        title: "Fixture docstring is not terminated",
        messageFormat: "Fixture '{0}' has an unterminated module docstring",
        category: "PySharp.Tests.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingKindField = new(
        id: "PYFIX003",
        title: "Fixture docstring has no :kind: field",
        messageFormat: "Fixture '{0}' docstring has no ':kind:' field; every fixture must declare ':kind: test' or ':kind: helper' explicitly",
        category: "PySharp.Tests.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidKindValue = new(
        id: "PYFIX004",
        title: "Invalid :kind: field value",
        messageFormat: "Fixture '{0}': ':kind:' must be 'test' or 'helper', got '{1}'",
        category: "PySharp.Tests.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RegressionFileName = new(
        id: "PYFIX005",
        title: "Fixture file name contains 'regression'",
        messageFormat: "Fixture '{0}': file names must not contain 'regression'; name the file after the behavior contract it verifies and move the origin story into the docstring",
        category: "PySharp.Tests.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnknownMetadataField = new(
        id: "PYFIX006",
        title: "Unknown fixture metadata field",
        messageFormat: "Fixture '{0}': unknown docstring metadata field ':{1}:'; extend PyFixtureTestsGenerator before using new fields",
        category: "PySharp.Tests.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor RegressionTitle = new(
        id: "PYFIX007",
        title: "Fixture docstring title starts with 'Regression'",
        messageFormat: "Fixture '{0}': the docstring title line starts with 'Regression'; state the behavior contract instead and move the origin story into ':background:'",
        category: "PySharp.Tests.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateTestMethodName = new(
        id: "PYFIX008",
        title: "Fixture test method name collision",
        messageFormat: "Fixture method name '{0}' collides between '{1}' and '{2}'",
        category: "PySharp.Tests.SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var fixtures = context.AdditionalTextsProvider
            .Where(static at => at.Path.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            .Select(static (at, ct) => (at.Path, Text: at.GetText(ct)?.ToString()))
            .Where(static tuple => tuple.Text is not null)
            .Where(static tuple => IsRootFixture(tuple.Path))
            .Select(static (tuple, ct) => FixtureInfo.Parse(tuple.Path, tuple.Text!))
            .Collect();

        context.RegisterSourceOutput(fixtures, static (context, fixtures) => Emit(context, fixtures));
    }

    /// <summary>
    /// Only fixtures directly inside test_pyfiles are entry tests; files in
    /// subdirectories are packages or support material and are never scanned.
    /// </summary>
    private static bool IsRootFixture(string path)
    {
        var normalized = path.Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        var directory = lastSlash < 0 ? string.Empty : normalized.Substring(0, lastSlash);
        return directory.EndsWith("test_pyfiles", StringComparison.OrdinalIgnoreCase)
            && (directory.Length == "test_pyfiles".Length
                || directory[directory.Length - "test_pyfiles".Length - 1] == '/');
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<FixtureInfo> fixtures)
    {
        foreach (var fixture in fixtures)
        {
            foreach (var diagnostic in fixture.Diagnostics)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    diagnostic.Descriptor, Location.None, diagnostic.MessageArgs));
            }
        }

        // Fixtures with error-severity diagnostics have no trustworthy kind,
        // so they emit no member; the build already fails on their report.
        // A method-name collision emits its first member (duplicates would
        // not compile) and reports one diagnostic per twin.
        var eligible = fixtures
            .Where(static f => f.Kind == FixtureKind.Test && !f.HasErrors && f.MethodName is not null)
            .ToArray();
        var tests = eligible
            .GroupBy(static f => f.MethodName)
            .Select(static g => g.OrderBy(static f => f.FileName, StringComparer.Ordinal).First())
            .OrderBy(static f => f.MethodName, StringComparer.Ordinal)
            .ToArray();
        foreach (var group in eligible.GroupBy(static f => f.MethodName).Where(static g => g.Count() > 1))
        {
            var members = group.OrderBy(static f => f.FileName, StringComparer.Ordinal).ToArray();
            for (var i = 1; i < members.Length; i++)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateTestMethodName, Location.None, group.Key, members[0].FileName, members[i].FileName));
            }
        }

        if (tests.Length == 0)
            return;

        var builder = new StringBuilder();
        builder.Append("// <auto-generated/>").Append('\n');
        builder.Append("#nullable enable").Append('\n');
        builder.Append("using Microsoft.VisualStudio.TestTools.UnitTesting;").Append('\n');
        builder.Append('\n');
        builder.Append("namespace ").Append(TestNamespace).Append(';').Append('\n');
        builder.Append('\n');
        builder.Append("/// <summary>").Append('\n');
        builder.Append("/// One test per Python fixture in test_pyfiles, generated by PyFixtureTestsGenerator").Append('\n');
        builder.Append("/// from the fixture docstring metadata. Fixture print output is for local").Append('\n');
        builder.Append("/// debugging only and is never asserted on.").Append('\n');
        builder.Append("/// </summary>").Append('\n');
        builder.Append("[TestClass]").Append('\n');
        builder.Append("public sealed class ").Append(TestClassName).Append('\n');
        builder.Append('{').Append('\n');
        foreach (var test in tests)
        {
            builder.Append("    [TestMethod]").Append('\n');
            builder.Append("    public void ").Append(test.MethodName).Append("() => ").Append(RunnerType)
                .Append(".Run(\"").Append(test.FileName).Append("\");").Append('\n');
            builder.Append('\n');
        }
        builder.Append('}').Append('\n');

        context.AddSource("PyFileTests.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    private enum FixtureKind
    {
        Helper,
        Test,
    }

    /// <summary>Parse result for one fixture: immutable and comparable so the incremental cache can hit.</summary>
    private sealed class FixtureInfo : IEquatable<FixtureInfo>
    {
        private static readonly Regex FieldRegex = new(@"^:([A-Za-z][A-Za-z0-9-]*):[ \t]?(.*)$", RegexOptions.Multiline);

        private FixtureInfo(string fileName, string? methodName, FixtureKind kind, ImmutableArray<(DiagnosticDescriptor Descriptor, string[] MessageArgs)> diagnostics)
        {
            FileName = fileName;
            MethodName = methodName;
            Kind = kind;
            Diagnostics = diagnostics;
        }

        public string FileName { get; }

        public string? MethodName { get; }

        public FixtureKind Kind { get; }

        public ImmutableArray<(DiagnosticDescriptor Descriptor, string[] MessageArgs)> Diagnostics { get; }

        internal bool HasErrors => Diagnostics.Any(static d => d.Descriptor.DefaultSeverity == DiagnosticSeverity.Error);

        public bool Equals(FixtureInfo? other) =>
            other is not null
            && FileName == other.FileName
            && Kind == other.Kind
            && Diagnostics.Length == other.Diagnostics.Length;

        public override bool Equals(object? obj) => Equals(obj as FixtureInfo);

        public override int GetHashCode() => FileName.GetHashCode();

        public static FixtureInfo Parse(string path, string text)
        {
            var normalized = path.Replace('\\', '/');
            var fileName = normalized.Substring(normalized.LastIndexOf('/') + 1);
            var diagnostics = ImmutableArray.CreateBuilder<(DiagnosticDescriptor, string[])>();

            if (fileName.Contains("regression", StringComparison.OrdinalIgnoreCase))
                diagnostics.Add((RegressionFileName, new[] { fileName }));

            // Resolve the method name even when the metadata is broken, so a
            // fixable file still collides by the name it will use.
            var methodName = ToMethodName(fileName);
            var kind = default(FixtureKind?);

            var docstring = TryGetDocstring(text, out var unterminated);
            if (unterminated)
            {
                diagnostics.Add((UnterminatedDocstring, new[] { fileName }));
            }
            else if (docstring is null)
            {
                diagnostics.Add((MissingModuleDocstring, new[] { fileName }));
            }
            else
            {
                ParseMetadata(fileName, docstring, diagnostics, ref kind);

                var title = docstring.Split('\n').FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line));
                if (title is not null && title.TrimStart().StartsWith("Regression", StringComparison.OrdinalIgnoreCase))
                    diagnostics.Add((RegressionTitle, new[] { fileName }));
            }

            if (kind is null)
            {
                // Only reached when the docstring was missing or unterminated;
                // the error is already reported, defaulting keeps the record valid.
                kind = FixtureKind.Test;
            }

            return new FixtureInfo(fileName, methodName, kind.Value, diagnostics.ToImmutable());
        }

        private static void ParseMetadata(
            string fileName,
            string docstring,
            ImmutableArray<(DiagnosticDescriptor, string[])>.Builder diagnostics,
            ref FixtureKind? kind)
        {
            foreach (Match field in FieldRegex.Matches(docstring))
            {
                var name = field.Groups[1].Value;
                var value = field.Groups[2].Value.Trim();
                switch (name)
                {
                    case "kind":
                        if (value == "test")
                            kind = FixtureKind.Test;
                        else if (value == "helper")
                            kind = FixtureKind.Helper;
                        else
                            diagnostics.Add((InvalidKindValue, new[] { fileName, value }));
                        break;
                    case "background":
                        // Free-form provenance text; no interpretation needed.
                        break;
                    default:
                        diagnostics.Add((UnknownMetadataField, new[] { fileName, name }));
                        break;
                }
            }
        }

        /// <summary>
        /// Returns the body of the leading module docstring, skipping a UTF-8
        /// BOM, leading whitespace and comment lines. Docstring escapes are not
        /// interpreted (the corpus does not use them in headers).
        /// </summary>
        private static string? TryGetDocstring(string text, out bool unterminated)
        {
            unterminated = false;
            var index = 0;
            if (text.Length > 0 && text[0] == '\uFEFF')
                index++;
            while (index < text.Length)
            {
                var ch = text[index];
                if (char.IsWhiteSpace(ch))
                {
                    index++;
                }
                else if (ch == '#')
                {
                    var end = text.IndexOf('\n', index);
                    if (end < 0)
                        return null;
                    index = end + 1;
                }
                else
                {
                    break;
                }
            }

            foreach (var delimiter in new[] { "\"\"\"", "'''" })
            {
                if (index + delimiter.Length > text.Length || text.Substring(index, delimiter.Length) != delimiter)
                    continue;
                var end = text.IndexOf(delimiter, index + delimiter.Length, StringComparison.Ordinal);
                if (end < 0)
                {
                    unterminated = true;
                    return null;
                }
                return text.Substring(index + delimiter.Length, end - index - delimiter.Length);
            }

            return null;
        }

        /// <summary>test_f_string_format.py → TestFStringFormat.</summary>
        private static string? ToMethodName(string fileName)
        {
            var stem = fileName.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - 3)
                : fileName;
            if (stem.StartsWith("test_", StringComparison.Ordinal))
                stem = stem.Substring(5);
            var builder = new StringBuilder("Test");
            foreach (var segment in stem.Split('_'))
            {
                if (segment.Length == 0)
                    continue;
                builder.Append(char.ToUpperInvariant(segment[0]));
                if (segment.Length > 1)
                    builder.Append(segment, 1, segment.Length - 1);
            }
            return builder.Length == 4 ? null : builder.ToString();
        }
    }
}

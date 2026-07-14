using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;

namespace PySharp.Analyzer.Internal;

partial class PySharpAnalyzerInternalAnalyzer
{
    /// <summary>
    /// PYSPI009 — Type name should follow PySharp naming convention.
    /// <para/>
    /// Triggers when a class inherits from <c>PyObject</c> but its name does not match
    /// the <c>Py&lt;Name&gt;Object</c> pattern, or when a class inherits from <c>PyTypeObject</c>
    /// but its name does not match the <c>Py&lt;Name&gt;ObjectType</c> pattern.
    /// <para/>
    /// Compliant (no diagnostic):
    /// <code>
    /// class PyIntObject : PyObject { }
    /// class PyIntObjectType : PyTypeObject&lt;PyIntObject&gt; { }
    /// class PyStrObject : PyObject { }
    /// class PyStrObjectType : PyTypeObject&lt;PyStrObject&gt; { }
    /// </code>
    /// Non-compliant:
    /// <code>
    /// class MyObject : PyObject { }                           // should be PyMyObject
    /// class IntType : PyTypeObject&lt;PyIntObject&gt; { }          // should be PyIntObjectType
    /// </code>
    /// Edge cases:
    /// <list type="bullet">
    ///   <item><description>Generic exception types (e.g., <c>PyExceptionType&lt;TSelf&gt;</c>) are exempt as known exceptions.</description></item>
    ///   <item><description>Only <c>class</c> declarations are checked; struct, interface, and record are ignored.</description></item>
    ///   <item><description>Indirect inheritance (e.g., inheriting from <c>PyIntObject</c> which inherits from <c>PyObject</c>) is still checked.</description></item>
    ///   <item><description><c>PyTypeObject</c> is checked first because it inherits from <c>PyObject</c> via <c>PyObjectManagedDict</c>.</description></item>
    /// </list>
    /// </summary>
    private static readonly DiagnosticDescriptor PYSPI009 = new(
        nameof(PYSPI009),
        "Type name should follow PySharp naming convention",
        "Type '{0}' inherits from {1} - name should match the pattern '{2}'",
        "PySharp",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Types inheriting from PyObject should be named 'Py<Name>Object', and types inheriting from PyTypeObject should be named 'Py<Name>ObjectType'.");

    private static readonly HashSet<string> KnownExceptions = new()
    {
        "PyObjectManagedDict",  // Public intermediate base between PyObject and PyTypeObject; provides dictionary attribute storage; naming is descriptive (PyObject + ManagedDict) rather than following Py&lt;Name&gt;Object convention
        "PyTypeObject",         // Non-generic + generic abstract base for type system; name follows PyObject convention not PyTypeObject convention
        "PyExceptionType",      // Abstract exception base; intentionally omits "Object"
        "UserDefinedType",      // Dynamic user-defined type; not a static built-in
        "PySharpException",     // Internal exception in PyResult; intentionally non-standard
        "TObject",              // Placeholder sentinel type in PyTypeObject.Declarations
    };

    private static void AnalyzeTypeNaming(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
        if (symbol is null)
            return;

        // Skip known exceptions
        if (IsKnownException(symbol))
            return;

        // Resolve base types from the compilation.
        var pyTypeObject = context.Compilation.GetTypeByMetadataName("PySharp.Modules.Builtins.PyTypeObject");
        var pyObject = context.Compilation.GetTypeByMetadataName("PySharp.Modules.Builtins.PyObject");

        // First check: inherits from PyTypeObject (directly or indirectly).
        // This must be checked before PyObject because PyTypeObject also inherits
        // from PyObject (via PyObjectManagedDict → PyObject).
        if (pyTypeObject is not null && InheritsFrom(symbol, pyTypeObject))
        {
            // Name must start with "Py" and end with "ObjectType".
            // This pattern covers: PyObjectType, PyIntObjectType, PyTypeObjectType, etc.
            if (!IsValidTypeObjectName(symbol.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    PYSPI009,
                    classDecl.Identifier.GetLocation(),
                    symbol.Name,
                    "PyTypeObject",
                    "Py<Name>ObjectType"));
            }
            return;
        }

        // Second check: inherits from PyObject (but not via PyTypeObject).
        if (pyObject is not null && InheritsFrom(symbol, pyObject))
        {
            // Name must start with "Py" and end with "Object" (but not "ObjectType").
            // This pattern covers: PyIntObject, PyStrObject, PyDictObject, etc.
            if (!IsValidObjectName(symbol.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    PYSPI009,
                    classDecl.Identifier.GetLocation(),
                    symbol.Name,
                    "PyObject",
                    "Py<Name>Object"));
            }
        }
    }

    /// <summary>
    /// Determines whether <paramref name="symbol"/> is a known exception that is allowed
    /// to deviate from the naming convention.
    /// </summary>
    private static bool IsKnownException(INamedTypeSymbol symbol)
    {
        var name = symbol.Name;

        // Check the static set first
        if (KnownExceptions.Contains(name))
            return true;

        // Handle generic variants of PyExceptionType:
        //   PyExceptionType<TSelf>
        //   PyExceptionType<TSelf, TBase>
        if (name == "PyExceptionType" && symbol.TypeParameters.Length > 0)
            return true;

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> inherits from
    /// <paramref name="baseType"/> (directly or indirectly).
    /// </summary>
    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Checks whether <paramref name="name"/> follows the <c>Py&lt;Name&gt;ObjectType</c> pattern.
    /// <para/>
    /// The name must start with <c>"Py"</c> and end with <c>"ObjectType"</c>.
    /// Examples of valid names: <c>PyObjectType</c>, <c>PyIntObjectType</c>, <c>PyTypeObjectType</c>.
    /// </summary>
    private static bool IsValidTypeObjectName(string name) =>
        name.StartsWith("Py") && name.EndsWith("ObjectType");

    /// <summary>
    /// Checks whether <paramref name="name"/> follows the <c>Py&lt;Name&gt;Object</c> pattern.
    /// <para/>
    /// The name must start with <c>"Py"</c>, end with <c>"Object"</c>, and must not end with <c>"ObjectType"</c>.
    /// Examples of valid names: <c>PyIntObject</c>, <c>PyStrObject</c>, <c>PyDictObject</c>.
    /// </summary>
    private static bool IsValidObjectName(string name) =>
        name.StartsWith("Py") && name.EndsWith("Object") && !name.EndsWith("ObjectType");
}

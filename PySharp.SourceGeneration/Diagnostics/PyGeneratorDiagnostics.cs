namespace PySharp.SourceGeneration.Diagnostics;

/// <summary>
/// Central definitions of the source generator's common attribute-argument error diagnostics.
/// Message wording aligns with .NET standard library exceptions (ArgumentNullException, ArgumentOutOfRangeException, ArgumentException).
/// Generators report them during the RegisterSourceOutput stage via <see cref="DiagnosticInfo.Report"/>.
/// </summary>
internal static class PyGeneratorDiagnostics
{
    /// <summary>Diagnostic category, matching the existing PYFZ001.</summary>
    private const string Category = "PySharp.SourceGeneration";

    internal static readonly DiagnosticDescriptor InvalidArgument = new(
        id: "PYARG001",
        title: "Invalid argument",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Value does not fall within the expected range.");

    /// <summary>
    /// PYARG002 — A required attribute argument is null.
    /// Aligns with <see cref="System.ArgumentNullException"/>: "Value cannot be null. (Parameter 'x')".
    /// </summary>
    internal static readonly DiagnosticDescriptor RequiredArgumentNull = new(
        id: "PYARG002",
        title: "Attribute argument is null",
        messageFormat: "Value cannot be null. (Parameter '{0}')",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A required constructor argument of a PySharp attribute was provided as null. The generator cannot produce valid output and would otherwise silently skip the member or emit invalid code.");

    /// <summary>
    /// PYARG003 — An enum argument value is outside the defined range.
    /// Aligns with <see cref="System.Enum.IsDefined"/> checks and <see cref="System.ArgumentOutOfRangeException"/>.
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidEnumValue = new(
        id: "PYARG003",
        title: "Invalid enum value for attribute argument",
        messageFormat: "Enum value '{0}' is not defined for type '{1}'. (Parameter '{2}')",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The value supplied for an enum-typed attribute argument is not a defined member of the enum type. Use one of the declared members.");

    /// <summary>
    /// PYARG004 — The argument type does not match, or the constant is invalid (Error TypedConstant).
    /// Aligns with compiler CS1503: "Argument 1: cannot convert from 'int' to 'string'".
    /// </summary>
    internal static readonly DiagnosticDescriptor InvalidArgumentType = new(
        id: "PYARG004",
        title: "Attribute argument has invalid type",
        messageFormat: "Argument '{0}': cannot convert from '{1}' to '{2}'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An attribute argument could not be interpreted as the expected type by the source generator, so the affected member cannot be generated.");

    /// <summary>
    /// PYARG005 — The .py file referenced by [PyFrozenModule] was not found as an AdditionalFile.
    /// </summary>
    internal static readonly DiagnosticDescriptor PyFileNotFound = new(
        id: "PYARG005",
        title: "Python file not found",
        messageFormat: "The Python file '{0}' specified by [PyFrozenModule] on '{1}' was not found as an AdditionalFile. Available .py additional files: [{2}]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The .py file referenced by the [PyFrozenModule] attribute is not listed as an AdditionalFile, so the frozen module cannot be generated.");

    /// <summary>
    /// PYARG006 — A method referenced by [PyExport] was not found on the containing type.
    /// </summary>
    internal static readonly DiagnosticDescriptor ExportMethodNotFound = new(
        id: "PYARG006",
        title: "Referenced method not found",
        messageFormat: "Method '{0}' referenced by [PyExport] on '{1}' was not found on type '{2}'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A nameof(...) method referenced by the [PyExport] attribute does not exist on the containing type, so the exported member cannot be generated.");

    /// <summary>
    /// PYARG007 — A method referenced by [PyExport] is missing [PyFunctionParameters].
    /// </summary>
    internal static readonly DiagnosticDescriptor ExportMethodMissingParameters = new(
        id: "PYARG007",
        title: "Method is missing [PyFunctionParameters]",
        messageFormat: "Method '{0}' referenced by [PyExport] on '{1}' must be annotated with [PyFunctionParameters].",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [PyExport] generator reads parameter definitions from the [PyFunctionParameters] attribute; without it the exported member cannot be generated.");

    /// <summary>
    /// PYARG008 — A method referenced by [PyExport] has a signature incompatible with PyFunction.
    /// </summary>
    internal static readonly DiagnosticDescriptor ExportMethodSignatureIncompatible = new(
        id: "PYARG008",
        title: "Method signature is not compatible with PyFunction",
        messageFormat: "Method '{0}' referenced by [PyExport] on '{1}' must have the signature 'PyResult(PyCallContext, PyArguments)'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A method referenced by the [PyExport] attribute must be convertible to the PyFunction delegate, i.e. return PyResult and take (PyCallContext, PyArguments).");

    /// <summary>
    /// PYARG009 — The name argument of [PyExport] is null or empty.
    /// </summary>
    internal static readonly DiagnosticDescriptor ExportNameNullOrEmpty = new(
        id: "PYARG009",
        title: "Exported name is null or empty",
        messageFormat: "The 'name' argument of [PyExport] on '{0}' cannot be null or empty.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The exported member needs a non-empty Python-facing name to be registered.");
}

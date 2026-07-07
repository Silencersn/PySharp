namespace PySharp.Runtime.PyAttributes;

/// <summary>
/// Marks a <see cref="PySharp.Modules.Builtins.PyFrozenModuleObject"/> subclass for source generation.
/// The generator reads the specified <c>.py</c> file at compile time and fills in the
/// <c>Code</c> property and constructor name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PyFrozenModuleAttribute : PyAttribute
{
    /// <summary>
    /// Marks a frozen module for source generation.
    /// </summary>
    /// <param name="moduleName">The Python module name (e.g. <c>"this"</c>).</param>
    /// <param name="pythonFilePath">
    /// Relative path to the Python source file from the project root (e.g. <c>"Lib/functools.py"</c>).
    /// The file must be listed as <c>&lt;AdditionalFiles&gt;</c> in the <c>.csproj</c>.
    /// </param>
    public PyFrozenModuleAttribute(string moduleName, string pythonFilePath)
    {
        ModuleName = moduleName;
        PythonFilePath = pythonFilePath;
    }

    /// <summary>The Python module name passed to the base constructor.</summary>
    public string ModuleName { get; }

    /// <summary>
    /// Relative path to the <c>.py</c> file from the project root.
    /// The file must be listed as <c>&lt;AdditionalFiles&gt;</c> in the <c>.csproj</c>.
    /// </summary>
    public string PythonFilePath { get; }
}

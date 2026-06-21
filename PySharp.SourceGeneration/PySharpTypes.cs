namespace PySharp.SourceGeneration;

internal static class PySharpTypes
{
    private const string AttributesNamespace = "PySharp.Runtime.PyAttributes";
    private const string BuiltinsNamespace = "PySharp.Modules.Builtins";
    public const string PySlotAttribute = $"{BuiltinsNamespace}.PyTypeObject.{nameof(PySlotAttribute)}";
    public const string PyTypeAttribute = $"{AttributesNamespace}.{nameof(PyTypeAttribute)}";
    public const string PyMethodAttribute = $"{AttributesNamespace}.{nameof(PyMethodAttribute)}";
    public const string PyClassMethodAttribute = $"{AttributesNamespace}.{nameof(PyClassMethodAttribute)}";
    public const string PyStaticMethodAttribute = $"{AttributesNamespace}.{nameof(PyStaticMethodAttribute)}";
    public const string PyPropertyAttribute = $"{AttributesNamespace}.{nameof(PyPropertyAttribute)}";
    public const string PyTypeConstructorAttribute = $"{AttributesNamespace}.{nameof(PyTypeConstructorAttribute)}";
    public const string PyTypeObjectOfT = $"{BuiltinsNamespace}.PyTypeObject`1";
}

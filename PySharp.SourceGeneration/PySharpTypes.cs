using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.SourceGeneration;

internal static class PySharpTypes
{
    private const string AttributesNamespace = "PySharp.Runtime.PyAttributes";
    public const string PySlotAttribute = $"{AttributesNamespace}.{nameof(PySlotAttribute)}";
    public const string PyTypeAttribute = $"{AttributesNamespace}.{nameof(PyTypeAttribute)}";
    public const string PyMethodAttribute = $"{AttributesNamespace}.{nameof(PyMethodAttribute)}";
    public const string PyPropertyAttribute = $"{AttributesNamespace}.{nameof(PyPropertyAttribute)}";

    private const string BuiltinsNamespace = "PySharp.Modules.Builtins";
    public const string PyTypeObjectOfT = $"{BuiltinsNamespace}.PyTypeObject`1";
}

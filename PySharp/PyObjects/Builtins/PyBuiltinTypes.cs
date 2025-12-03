namespace PySharp.PyObjects.Builtins;

public static class PyBuiltinTypes
{
    public static readonly PyObjectType Object = new(); // object
    public static readonly PyStrObjectType Str = new(); // str
    public static readonly PyIntObjectType Int = new(); // int
    public static readonly PyFloatObjectType Float = new(); // float
    public static readonly PyTupleObjectType Tuple = new(); // tuple
    public static readonly PyDictObjectType Dict = new(); // dict
    public static readonly PyBoolObjectType Bool = new(); // bool
    public static readonly PyListObjectType List = new(); // list
    public static readonly PyTypeObjectType Type = new(); // type
    public static readonly PyEllipsisObjectType Ellipsis = new(); // Ellipsis
    public static readonly PyMethodObjectType Method = new(); // method
    public static readonly PyRangeObjectType Range = new(); // range
    public static readonly PyModuleObjectType Module = new(); // module
    public static readonly PyZipObjectType Zip = new(); // zip
    public static readonly PyPropertyObjectType Property = new(); // property
    internal static readonly PyMethodDescriptorObjectType MethodDescriptor = new();
}
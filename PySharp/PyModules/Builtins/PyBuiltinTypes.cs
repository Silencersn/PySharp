namespace PySharp.PyModules.Builtins;

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
    public static readonly PyRangeObjectType Range = new(); // range
    public static readonly PyModuleObjectType Module = new(); // module
    public static readonly PyZipObjectType Zip = new(); // zip
    public static readonly PySuperObjectType Super = new(); // super

    public static readonly PyPropertyObjectType Property = PyPropertyObjectType.Shared; // property
    public static readonly PyFunctionObjectType Function = PyFunctionObjectType.Shared; // function
    public static readonly PyMethodObjectType Method = PyMethodObjectType.Shared; // method
    public static readonly PyMethodDescriptorObjectType MethodDescriptor = PyMethodDescriptorObjectType.Shared; // method_descriptor
}
namespace PySharp.PyModules.Builtins;

public class PyBuiltinsModuleObject : PyModuleObject
{
    public override string? ReprPrompt => "(built-in)";

    public PyBuiltinsModuleObject() : base("builtins")
    {
        AddObjToAttrs(PyBuiltinFunctions.Print); // print
        AddObjToAttrs(PyBuiltinFunctions.Repr); // repr
        AddObjToAttrs(PyBuiltinFunctions.Hash); // hash
        AddObjToAttrs(PyBuiltinFunctions.Len); // len
        AddObjToAttrs(PyBuiltinFunctions.Abs); // abs
        AddObjToAttrs(PyBuiltinFunctions.Iter); // iter
        AddObjToAttrs(PyBuiltinFunctions.Next); // next
        AddObjToAttrs(PyBuiltinFunctions.Pow); // pow
        AddObjToAttrs(PyBuiltinFunctions.DivMod); // divmod
        AddObjToAttrs(PyBuiltinFunctions.Input); // input
        AddObjToAttrs(PyBuiltinFunctions.Eval); // eval
        AddObjToAttrs(PyBuiltinFunctions.Exec); // exec
        AddObjToAttrs(PyBuiltinFunctions.All); // all
        AddObjToAttrs(PyBuiltinFunctions.Any); // any
        AddObjToAttrs(PyBuiltinFunctions.Max); // max
        AddObjToAttrs(PyBuiltinFunctions.Min); // min
        AddObjToAttrs(PyBuiltinFunctions.Sum); // sum
        AddObjToAttrs(PyBuiltinFunctions.GetAttr); // getattr
        AddObjToAttrs(PyBuiltinFunctions.SetAttr); // setattr
        AddObjToAttrs(PyBuiltinFunctions.HasAttr); // hasattr
        AddObjToAttrs(PyBuiltinFunctions.Dir); // dir
        AddObjToAttrs(PyBuiltinFunctions.Chr); // chr
        AddObjToAttrs(PyBuiltinFunctions.Ord); // ord
        AddObjToAttrs(PyBuiltinFunctions.Locals); // locals
        AddObjToAttrs(PyBuiltinFunctions.Globals); // globals
        AddObjToAttrs(PyBuiltinFunctions.Import); // __import__
        AddObjToAttrs(PyBuiltinFunctions.IsInstance); // isinstance
        AddObjToAttrs(PyBuiltinFunctions.IsSubclass); // issubclass
        AddObjToAttrs(PyBuiltinFunctions.Callable); // callable
        AddObjToAttrs(PyBuiltinFunctions.Id); // id

        AddObjToAttrs(PyObjectType.Shared); // object
        AddObjToAttrs(PyStrObjectType.Shared); // str
        AddObjToAttrs(PyIntObjectType.Shared); // int
        AddObjToAttrs(PyFloatObjectType.Shared); // float
        AddObjToAttrs(PyTupleObjectType.Shared); // tuple
        AddObjToAttrs(PyDictObjectType.Shared); // dict
        AddObjToAttrs(PyBoolObjectType.Shared); // bool
        AddObjToAttrs(PyListObjectType.Shared); // list
        AddObjToAttrs(PyTypeObjectType.Shared); // type
        AddObjToAttrs(PyRangeObjectType.Shared); // range
        AddObjToAttrs(PyZipObjectType.Shared); // zip
        AddObjToAttrs(PyPropertyObjectType.Shared); // property
        AddObjToAttrs(PySuperObjectType.Shared); // super
        AddObjToAttrs(PySliceObjectType.Shared); // slice
        AddObjToAttrs(PyMapObjectType.Shared); // map
        AddObjToAttrs(PySetObjectType.Shared); // set
        AddObjToAttrs(PyComplexObjectType.Shared); // complex
        AddObjToAttrs(PyStaticMethodObjectType.Shared); // staticmethod
        AddObjToAttrs(PyClassMethodObjectType.Shared); // classmethod

        AddObjToAttrs("Ellipsis", PyEllipsisObject.Ellipsis); // Ellipsis

        AddObjToAttrs(PyStandardExceptionTypes.BaseException); // BaseException
        AddObjToAttrs(PyStandardExceptionTypes.SystemExit); // SystemExit
        AddObjToAttrs(PyStandardExceptionTypes.Exception); // Exception
        AddObjToAttrs(PyStandardExceptionTypes.TypeError); // TypeError
        AddObjToAttrs(PyStandardExceptionTypes.StopIteration); // StopIteration
        AddObjToAttrs(PyStandardExceptionTypes.AttributeError); // AttributeError
        AddObjToAttrs(PyStandardExceptionTypes.LookupError); // LookupError
        AddObjToAttrs(PyStandardExceptionTypes.KeyError); // KeyError
        AddObjToAttrs(PyStandardExceptionTypes.IndexError); // IndexError
        AddObjToAttrs(PyStandardExceptionTypes.ValueError); // ValueError
        AddObjToAttrs(PyStandardExceptionTypes.NameError); // NameError
        AddObjToAttrs(PyStandardExceptionTypes.ImportError); // ImportError
        AddObjToAttrs(PyStandardExceptionTypes.ModuleNotFoundError); // ModuleNotFoundError
        AddObjToAttrs(PyStandardExceptionTypes.SyntaxError); // SyntaxError
        AddObjToAttrs(PyStandardExceptionTypes.IndentationError); // IndentationError
        AddObjToAttrs(PyStandardExceptionTypes.ArithmeticError); // ArithmeticError
        AddObjToAttrs(PyStandardExceptionTypes.ZeroDivisionError); // ZeroDivisionError
        AddObjToAttrs(PyStandardExceptionTypes.AssertionError); // AssertionError
        AddObjToAttrs(PyStandardExceptionTypes.UnboundLocalError); // UnboundLocalError
        AddObjToAttrs(PyStandardExceptionTypes.RuntimeError); // RuntimeError
        AddObjToAttrs(PyStandardExceptionTypes.GeneratorExit); // GeneratorExit
    }
}

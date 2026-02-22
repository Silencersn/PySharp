namespace PySharp.Modules.Builtins;

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
        AddObjToAttrs(PyBuiltinFunctions.Bin); // bin
        AddObjToAttrs(PyBuiltinFunctions.Oct); // oct
        AddObjToAttrs(PyBuiltinFunctions.Hex); // hex
        AddObjToAttrs(PyBuiltinFunctions.Ascii); // ascii
        AddObjToAttrs(PyBuiltinFunctions.Format); // format
        AddObjToAttrs(PyBuiltinFunctions.DelAttr); // delattr
        AddObjToAttrs(PyBuiltinFunctions.Compile); // compile

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

        AddObjToAttrs(PyBaseExceptionObjectType.Shared); // BaseException
        AddObjToAttrs(PySystemExitObjectType.Shared); // SystemExit
        AddObjToAttrs(PyExceptionObjectType.Shared); // Exception
        AddObjToAttrs(PyTypeErrorObjectType.Shared); // TypeError
        AddObjToAttrs(PyStopIterationObjectType.Shared); // StopIteration
        AddObjToAttrs(PyAttributeErrorObjectType.Shared); // AttributeError
        AddObjToAttrs(PyLookupErrorObjectType.Shared); // LookupError
        AddObjToAttrs(PyKeyErrorObjectType.Shared); // KeyError
        AddObjToAttrs(PyIndexErrorObjectType.Shared); // IndexError
        AddObjToAttrs(PyValueErrorObjectType.Shared); // ValueError
        AddObjToAttrs(PyNameErrorObjectType.Shared); // NameError
        AddObjToAttrs(PyImportErrorObjectType.Shared); // ImportError
        AddObjToAttrs(PyModuleNotFoundErrorObjectType.Shared); // ModuleNotFoundError
        AddObjToAttrs(PySyntaxErrorObjectType.Shared); // SyntaxError
        AddObjToAttrs(PyIndentationErrorObjectType.Shared); // IndentationError
        AddObjToAttrs(PyArithmeticErrorObjectType.Shared); // ArithmeticError
        AddObjToAttrs(PyZeroDivisionErrorObjectType.Shared); // ZeroDivisionError
        AddObjToAttrs(PyAssertionErrorObjectType.Shared); // AssertionError
        AddObjToAttrs(PyUnboundLocalErrorObjectType.Shared); // UnboundLocalError
        AddObjToAttrs(PyRuntimeErrorObjectType.Shared); // RuntimeError
        AddObjToAttrs(PyGeneratorExitObjectType.Shared); // GeneratorExit

        AddObjToAttrs(PyBaseExceptionGroupObjectType.Shared); // BaseExceptionGroup
        AddObjToAttrs(PyExceptionGroupObjectType.Shared); // ExceptionGroup
    }
}

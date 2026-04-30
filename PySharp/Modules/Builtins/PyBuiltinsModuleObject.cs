namespace PySharp.Modules.Builtins;

public class PyBuiltinsModuleObject : PyModuleObject
{
    public override string? Origin => "built-in";

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
        AddObjToAttrs(PyBuiltinFunctions.Sorted); // sorted
        AddObjToAttrs(PyBuiltinFunctions.Round); // round
        AddObjToAttrs(PyBuiltinFunctions.Vars); // vars

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
        AddObjToAttrs(PyFrozenSetObjectType.Shared); // frozenset
        AddObjToAttrs(PyComplexObjectType.Shared); // complex
        AddObjToAttrs(PyStaticMethodObjectType.Shared); // staticmethod
        AddObjToAttrs(PyClassMethodObjectType.Shared); // classmethod
        AddObjToAttrs(PyEnumerateObjectType.Shared); // enumerate
        AddObjToAttrs(PyReversedObjectType.Shared); // reversed
        AddObjToAttrs(PyFilterObjectType.Shared); // filter
        AddObjToAttrs(PyBytesObjectType.Shared); // bytes
        AddObjToAttrs(PyByteArrayObjectType.Shared); // bytearray

        AddObjToAttrs("Ellipsis", PyEllipsisObject.Ellipsis); // Ellipsis
        AddObjToAttrs("NotImplemented", PyNotImplementedObject.NotImplemented); // NotImplemented

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

        AddObjToAttrs(PyKeyboardInterruptObjectType.Shared); // KeyboardInterrupt
        AddObjToAttrs(PyFloatingPointErrorObjectType.Shared); // FloatingPointError
        AddObjToAttrs(PyOverflowErrorObjectType.Shared); // OverflowError
        AddObjToAttrs(PyBufferErrorObjectType.Shared); // BufferError
        AddObjToAttrs(PyEOFErrorObjectType.Shared); // EOFError
        AddObjToAttrs(PyMemoryErrorObjectType.Shared); // MemoryError
        AddObjToAttrs(PyOSErrorObjectType.Shared); // OSError
        AddObjToAttrs(PyBlockingIOErrorObjectType.Shared); // BlockingIOError
        AddObjToAttrs(PyChildProcessErrorObjectType.Shared); // ChildProcessError
        AddObjToAttrs(PyConnectionErrorObjectType.Shared); // ConnectionError
        AddObjToAttrs(PyBrokenPipeErrorObjectType.Shared); // BrokenPipeError
        AddObjToAttrs(PyConnectionAbortedErrorObjectType.Shared); // ConnectionAbortedError
        AddObjToAttrs(PyConnectionRefusedErrorObjectType.Shared); // ConnectionRefusedError
        AddObjToAttrs(PyConnectionResetErrorObjectType.Shared); // ConnectionResetError
        AddObjToAttrs(PyFileExistsErrorObjectType.Shared); // FileExistsError
        AddObjToAttrs(PyFileNotFoundErrorObjectType.Shared); // FileNotFoundError
        AddObjToAttrs(PyInterruptedErrorObjectType.Shared); // InterruptedError
        AddObjToAttrs(PyIsADirectoryErrorObjectType.Shared); // IsADirectoryError
        AddObjToAttrs(PyNotADirectoryErrorObjectType.Shared); // NotADirectoryError
        AddObjToAttrs(PyPermissionErrorObjectType.Shared); // PermissionError
        AddObjToAttrs(PyProcessLookupErrorObjectType.Shared); // ProcessLookupError
        AddObjToAttrs(PyTimeoutErrorObjectType.Shared); // TimeoutError
        AddObjToAttrs(PyReferenceErrorObjectType.Shared); // ReferenceError
        AddObjToAttrs(PyNotImplementedErrorObjectType.Shared); // NotImplementedError
        AddObjToAttrs(PyPythonFinalizationErrorObjectType.Shared); // PythonFinalizationError
        AddObjToAttrs(PyRecursionErrorObjectType.Shared); // RecursionError
        AddObjToAttrs(PyStopAsyncIterationObjectType.Shared); // StopAsyncIteration
        AddObjToAttrs(PyTabErrorObjectType.Shared); // TabError
        AddObjToAttrs(PySystemErrorObjectType.Shared); // SystemError
        AddObjToAttrs(PyUnicodeErrorObjectType.Shared); // UnicodeError
        AddObjToAttrs(PyUnicodeEncodeErrorObjectType.Shared); // UnicodeEncodeError
        AddObjToAttrs(PyUnicodeDecodeErrorObjectType.Shared); // UnicodeDecodeError
        AddObjToAttrs(PyUnicodeTranslateErrorObjectType.Shared); // UnicodeTranslateError

        AddObjToAttrs(PyWarningObjectType.Shared); // Warning
        AddObjToAttrs(PyUserWarningObjectType.Shared); // UserWarning
        AddObjToAttrs(PySyntaxWarningObjectType.Shared); // SyntaxWarning
        AddObjToAttrs(PyDeprecationWarningObjectType.Shared); // DeprecationWarning
        AddObjToAttrs(PyBytesWarningObjectType.Shared); // BytesWarning
        AddObjToAttrs(PyEncodingWarningObjectType.Shared); // EncodingWarning
        AddObjToAttrs(PyFutureWarningObjectType.Shared); // FutureWarning
        AddObjToAttrs(PyImportWarningObjectType.Shared); // ImportWarning
        AddObjToAttrs(PyPendingDeprecationWarningObjectType.Shared); // PendingDeprecationWarning
        AddObjToAttrs(PyResourceWarningObjectType.Shared); // ResourceWarning
        AddObjToAttrs(PyRuntimeWarningObjectType.Shared); // RuntimeWarning
        AddObjToAttrs(PyUnicodeWarningObjectType.Shared); // UnicodeWarning
    }
}

namespace PySharp.PyModules.Builtins;

public class PyBuiltinsModuleObject : PyModuleObject
{
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

        AddObjToAttrs(PyBuiltinTypes.Object); // object
        AddObjToAttrs(PyBuiltinTypes.Str); // str
        AddObjToAttrs(PyBuiltinTypes.Int); // int
        AddObjToAttrs(PyBuiltinTypes.Float); // float
        AddObjToAttrs(PyBuiltinTypes.Tuple); // tuple
        AddObjToAttrs(PyBuiltinTypes.Dict); // dict
        AddObjToAttrs(PyBuiltinTypes.Bool); // bool
        AddObjToAttrs(PyBuiltinTypes.List); // list
        AddObjToAttrs(PyBuiltinTypes.Type); // type
        AddObjToAttrs(PyBuiltinTypes.Range); // range
        AddObjToAttrs(PyBuiltinTypes.Zip); // zip
        AddObjToAttrs(PyBuiltinTypes.Property); // property
        AddObjToAttrs(PyBuiltinTypes.Super); // super
        AddObjToAttrs(PyBuiltinTypes.Slice); // slice

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
    }

    public override PyObject? Repr()
    {
        return PyStrObject.FromString($"<module '{Name}' (built-in)>");
    }
}

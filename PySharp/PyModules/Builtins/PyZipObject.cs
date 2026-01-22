using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Builtins;

public class PyZipObject : PyObject
{
    internal readonly PyObject[] _iterables;
    internal readonly bool _strict;
    internal bool _end;

    public override PyTypeObject DefaultPyType => PyZipObjectType.Shared;

    internal PyZipObject(IEnumerable<PyObject> iterables, bool strict)
    {
        _iterables = [.. iterables];
        _strict = strict;
        _end = false;
    }
}

public sealed class PyZipObjectType : PyTypeObject<PyZipObjectType, PyZipObject>
{
    public override string Module => "builtins";
    public override string Name => "zip";

    private static readonly PyBuiltinFunctionOrMethodObject _new = PyBuiltinFunctionOrMethodObject.CreateFunction(PySpecialNames.New, NewImpl);

    [PyFunctionArgsDef("*iterables", "strict=False")]
    private static PyResult NewImpl(PyCallContext context, PyArguments arguments)
    {
        var result = PySpecialMethods.Bool(context, arguments["strict"]);
        if (result.IsError)
            return result;

        List<PyObject> iterables = [];
        foreach (var arg in arguments.ExtraArgs)
        {
            var iter = PySpecialMethods.Iter(context, arg);
            if (iter.IsError)
                return iter;
            iterables.Add(iter.Value);
        }

        return new PyZipObject(iterables, result.Value.BoolValue);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        var obj = _new.Call(context, args, kwargs);
        if (obj.IsError)
            return obj;
        obj.Value._pyType = cls;
        return obj;
    }

    protected override PyResult Iter(PyCallContext context, PyZipObject self)
    {
        return self;
    }

    protected override PyResult Next(PyCallContext context, PyZipObject self)
    {
        if (self._end)
            return PyResult.RaiseStopIteration();

        if (self._iterables.Length is 0)
        {
            self._end = true;
            return PyTupleObject.CreateTuple();
        }

        var list = new List<PyObject>();
        var allHaveItem = true;
        var allNoItem = false;
        for (int i = 0; i < self._iterables.Length; i++)
        {
            var iterable = self._iterables[i];
            var item = PySpecialMethods.Next(context, iterable);
            if (item.IsError)
            {
                if (item.IsStopIteration)
                {
                    allHaveItem = false;
                    self._end = true;
                    if (!allNoItem)
                    {
                        if (i is 0)
                        {
                            allNoItem = true;
                        }
                        else if (self._strict)
                        {
                            if (i is 1)
                                return PyResult.ValueError(PySR.Runtime_Zip_SecondShorterThanFirst);
                            return PyResult.ValueError(PySR.Runtime_Zip_NthShorterThanPrevious, i + 1, i);
                        }
                    }
                }
                else
                {
                    return item;
                }
            }
            else
            {
                if (allNoItem && self._strict)
                {
                    if (i is 1)
                        return PyResult.ValueError(PySR.Runtime_Zip_SecondLongerThanFirst);
                    return PyResult.ValueError(PySR.Runtime_Zip_NthLongerThanPrevious, i + 1, i);
                }
                list.Add(item.Value);
            }
        }

        if (!allHaveItem)
            return PyResult.RaiseStopIteration();

        return PyTupleObject.CreateTuple(list);
    }
}
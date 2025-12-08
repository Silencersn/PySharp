using PySharp.PyRuntime;
using PySharp.PyRuntime.Calls;
using PySharp.PyRuntime.PyAttributes;

namespace PySharp.PyModules.Builtins;

public class PyZipObject : PyObject
{
    private readonly PyObject[] _iterables;
    private readonly bool _strict;
    private bool _end;

    internal PyZipObject(IEnumerable<PyObject> iterables, bool strict)
    {
        _iterables = [.. iterables];
        _strict = strict;
        _end = false;
    }

    public override PyObject? Iter()
    {
        return this;
    }

    public override PyObject? Next()
    {
        if (_end)
            return PyVirtualMachine.RaiseStopIteration();

        if (_iterables.Length is 0)
        {
            _end = true;
            return PyTupleObject.CreateTuple();
        }

        var list = new List<PyObject>();
        var allHaveItem = true;
        var allNoItem = false;
        for (int i = 0; i < _iterables.Length; i++)
        {
            var iterable = _iterables[i];
            var item = iterable.Next();
            if (item is null)
            {
                if (PyVirtualMachine.IsExceptionOfTypeRaised(PyStandardExceptionTypes.StopIteration))
                {
                    PyVirtualMachine.ClearException();
                    allHaveItem = false;
                    _end = true;
                    if (!allNoItem)
                    {
                        if (i is 0)
                        {
                            allNoItem = true;
                        }
                        else if (_strict)
                        {
                            return PyVirtualMachine.RaiseValueError($"zip() argument {i + 1} is shorter than {(i > 1 ? $"arguments 1-{i}" : "argument 1")}");
                        }
                    }
                }
                else
                {
                    return null;
                }
            }
            else
            {
                if (allNoItem && _strict)
                    return PyVirtualMachine.RaiseValueError($"zip() argument {i + 1} is longer than {(i > 1 ? $"arguments 1-{i}" : "argument 1")}");

                list.Add(item);
            }
        }

        if (!allHaveItem)
            return PyVirtualMachine.RaiseStopIteration();

        return PyTupleObject.CreateTuple(list);
    }
}

public sealed class PyZipObjectType : PyTypeObject
{
    public override string Name => "zip";

    private static readonly PyBuiltinFunctionOrMethodObject _new = new(PySpecialNames.New, ZipImpl);

    [PyFunctionArgsDef("*iterables", "strict=False")]
    private static PyZipObject? ZipImpl(PyArguments arguments)
    {
        if (!PySpecialMethods.TryGetBool(arguments.Kwargs["strict"], out var b))
            return null;

        List<PyObject> iterables = [];
        foreach (var arg in arguments.ExtraArgs)
        {
            var iter = arg.Iter();
            if (iter is null)
                return null;

            iterables.Add(iter);
        }

        return new PyZipObject(iterables, b.BoolValue);
    }

    public override PyObject? New(IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        return _new.Call(args, kwargs);
    }
}
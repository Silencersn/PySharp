using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

public sealed class PyBaseExceptionGroupObjectType : PyExceptionType<PyBaseExceptionGroupObjectType, PyBaseExceptionObjectType>
{
    public override string Module => "builtins";
    public override string Name => "BaseExceptionGroup";

    public PyBaseExceptionGroupObjectType()
    {
        AppendMethodDescriptor("derive", Derive);
        AppendMethodDescriptor("split", Split);
    }

    internal static PyExceptionObject CreateExceptionGroup(string message, IEnumerable<PyExceptionObject> excs)
    {
        var info = new ExceptionGroupInfo(message, [.. excs]);
        PyTypeObject type = Shared;
        if (info.Exceptions.All(static exc => PyExceptionObjectType.Shared.IsInstance(exc)))
            type = PyExceptionGroupObjectType.Shared;
        return new PyExceptionObject(type, [PyStrObject.FromString(message), .. excs]) { AsGroup = info };
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!TryParseExceptionGroupInfo(this, args, kwargs, out var info, out var err))
            return err.Value;

        PyTypeObject type = cls;

        if (type.IsSubclassOf(PyExceptionGroupObjectType.Shared))
        {
            if (!info.Exceptions.All(PyExceptionObjectType.Shared.IsInstance))
            {
                if (type is PyExceptionGroupObjectType)
                    return PyResult.TypeError(PySR.Runtime_ExceptionGroup_NestBaseExceptionsForExceptionGroup);

                return PyResult.TypeError(PySR.Runtime_ExceptionGroup_NestBaseExceptions, type.FullName);
            }
        }

        if (type is PyBaseExceptionGroupObjectType)
        {
            if (info.Exceptions.All(static exc => PyExceptionObjectType.Shared.IsInstance(exc)))
                type = PyExceptionGroupObjectType.Shared;
        }

        return new PyExceptionObject(type, args) { AsGroup = info };
    }

    protected override PyResult Str(PyCallContext context, PyExceptionObject self)
    {
        if (self.AsGroup is null)
            return PyResult.TypeError(null);

        var count = self.AsGroup.Exceptions.Count;
        Debug.Assert(count > 0);
        if (count is 1)
            return PyStrObject.FromString($"{self.AsGroup.Message} (1 sub-exception)");
        return PyStrObject.FromString($"{self.AsGroup.Message} ({count} sub-exceptions)");
    }

    [PyFunctionArgsDef("excs", "/")]
    internal PyResult Derive(PyCallContext context, PyExceptionObject self, PyArguments arguments)
    {
        if (!self.IsGroup)
            return PyResult.TypeError(null);

        if (!TryParseExceptions(arguments[0], out var excs, out var err))
            return err.Value;

        var info = new ExceptionGroupInfo(self.AsGroup.Message, [.. excs]);
        var result = new PyExceptionObject(self.PyType, [PyStrObject.FromString(info.Message), PyListObject.CreateList(info.Exceptions)])
        {
            Traceback = self.Traceback,
            Cause = self.Cause,
            Context = self.Context,
            AsGroup = info
        };

        if (result.AsGroup.Exceptions.All(PyExceptionObjectType.Shared.IsInstance))
            result._pyType = PyExceptionGroupObjectType.Shared;
        else
            result._pyType = Shared;

        return result;
    }

    [PyFunctionArgsDef("condition", "/")]
    internal PyResult Split(PyCallContext context, PyExceptionObject self, PyArguments arguments)
    {
        if (!self.IsGroup)
            return PyResult.TypeError(null);

        var conditionObj = arguments[0];
        Func<PyExceptionObject, PyResult<PyBoolObject>> predicate;
        if (conditionObj is PyTypeObject type)
        {
            if (!type.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                return PyResult.TypeError(PySR.Runtime_ExceptionGroup_SplitExpectedCondition);

            predicate = exc => PyBoolObject.FromBoolean(type.IsInstance(exc));
        }
        else if (conditionObj is PyTupleObject tuple)
        {
            List<PyTypeObject> types = [];
            foreach (var o in tuple._array)
            {
                if (o is not PyTypeObject t || !t.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                    return PyResult.TypeError(PySR.Runtime_ExceptionGroup_SplitExpectedCondition);

                types.Add(t);
            }

            predicate = exc => PyBoolObject.FromBoolean(types.Any(type => type.IsInstance(exc)));
        }
        else
        {
            predicate = exc =>
            {
                var result = conditionObj.Call(context, [exc]);
                if (result.IsError)
                    return result.Of<PyBoolObject>();

                return PySpecialMethods.Bool(context, result.Value);
            };
        }

        (var err, PyObject? match, PyObject? rest) = SplitImpl(self);
        if (err is not null)
            return err.Value;

        match ??= PyNoneObject.None;
        rest ??= PyNoneObject.None;

        return PyTupleObject.CreateTuple(match, rest);


        (PyResult? Error, PyExceptionObject? MatchGroup, PyExceptionObject? RestGroup) SplitImpl(PyExceptionObject exceptionGroup)
        {
            if (!exceptionGroup.IsGroup)
                return ReturnError(PyResult.TypeError(null));

            List<PyExceptionObject> match = [];
            List<PyExceptionObject> rest = [];

            foreach (var subException in exceptionGroup.AsGroup.Exceptions)
            {
                if (subException.IsGroup)
                {
                    var (err, subMatch, subRest) = SplitImpl(subException);
                    if (err is not null)
                        return ReturnError(err.Value);

                    if (subMatch is not null)
                        match.Add(subMatch);

                    if (subRest is not null)
                        rest.Add(subRest);
                }
                else
                {
                    var matched = predicate(subException);
                    if (matched.IsError)
                        return ReturnError(matched);

                    if (matched.Value.BoolValue)
                        match.Add(subException);
                    else
                        rest.Add(subException);
                }
            }

            PyExceptionObject? matchGroup = null;
            if (match.Count > 0)
            {
                var matchResult = Derive(match);
                if (matchResult.IsError)
                    return ReturnError(matchResult);
                matchGroup = matchResult.Value;
            }

            PyExceptionObject? restGroup = null;
            if (rest.Count > 0)
            {
                var restResult = Derive(rest);
                if (restResult.IsError)
                    return ReturnError(restResult);
                restGroup = restResult.Value;
            }

            return (null, matchGroup, restGroup);

            static (PyResult?, PyExceptionObject?, PyExceptionObject?) ReturnError(PyResult error)
            {
                return (error, null, null);
            }

            PyResult<PyExceptionObject> Derive(List<PyExceptionObject> excs)
            {
                Debug.Assert(excs.Count > 0);

                var list = PyListObject.CreateList(excs);
                var result = exceptionGroup.CallMethod(context, "derive", [list]);
                if (result.IsError)
                    return result.Of<PyExceptionObject>();

                if (result.Value is not PyExceptionObject exc || !exc.IsGroup || !Shared.IsInstance(result.Value))
                    return PyResult.TypeError(PySR.Runtime_ExceptionGroup_DeriveReturnNonGroup).Of<PyExceptionObject>();

                return exc;
            }
        }
    }

    internal static bool TryParseExceptions(PyObject excsObj,
        [NotNullWhen(true)] out IEnumerable<PyExceptionObject>? exceptions, [NotNullWhen(false)] out PyResult? err)
    {
        exceptions = null;

        IReadOnlyList<PyObject>? excs = excsObj switch
        {
            PyListObject list => list._list,
            PyTupleObject tuple => tuple._array,
            _ => null
        };

        if (excs is null)
        {
            err = PyResult.TypeError(PySR.Runtime_ExceptionGroup_NewGroup_ExcsNonSeq);
            return false;
        }

        if (excs.Count is 0)
        {
            err = PyResult.TypeError(PySR.Runtime_ExceptionGroup_NewGroup_ExcsEmpty);
            return false;
        }

        for (var i = 0; i < excs.Count; i++)
        {
            if (excs[i] is not PyExceptionObject)
            {
                err = PyResult.ValueError(PySR.Runtime_ExceptionGroup_NewGroup_ExcsItemNonExc, i + 1);
                return false;
            }
        }

        exceptions = excs.Cast<PyExceptionObject>();
        err = null;
        return true;
    }

    internal static bool TryParseExceptionGroupInfo(PyTypeObject exceptionGroupType, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs,
        [NotNullWhen(true)] out ExceptionGroupInfo? info, [NotNullWhen(false)] out PyResult? err)
    {
        info = null;

        if (!PyArgsValidator.ValidateArgs(args, 2, out err))
            return false;

        if (!PyArgsValidator.ValidateEmptyKwargs(kwargs, out err))
            return false;

        if (args[0] is not PyStrObject msg)
        {
            err = PyResult.TypeError(PySR.Runtime_ExceptionGroup_NewGroup_MsgNonStr, exceptionGroupType.FullName, args[0].PyType.FullName);
            return false;
        }

        if (!TryParseExceptions(args[1], out var excs, out err))
            return false;

        info = new ExceptionGroupInfo(msg.Value, [.. excs]);
        return true;
    }
}

public sealed class PyExceptionGroupObjectType : PyExceptionType<PyExceptionGroupObjectType>
{
    public override IReadOnlyList<PyTypeObject> Bases => [PyBaseExceptionGroupObjectType.Shared, PyExceptionObjectType.Shared];
    public override string Module => "builtins";
    public override string Name => "ExceptionGroup";
}
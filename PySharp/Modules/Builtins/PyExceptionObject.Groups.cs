using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

[PyException("BaseExceptionGroup", Bases = [typeof(PyBaseExceptionObjectType)])]
public sealed partial class PyBaseExceptionGroupObjectType : PyExceptionType
{
    internal static PyExceptionObject CreateExceptionGroup(string message, IEnumerable<PyExceptionObject> excs)
    {
        var info = new ExceptionGroupInfo(message, [.. excs]);
        PyTypeObject type = Shared;
        if (info.Exceptions.All(static exc => PyExceptionObjectType.Shared.IsInstance(exc)))
            type = PyExceptionGroupObjectType.Shared;
        return PyExceptionObject.UnsafeCreate(type, [PyStrObject.FromString(message), .. excs], info);
    }

    protected override PyResult New(PyCallContext context, PyTypeObject cls, IReadOnlyList<PyObject> args, IReadOnlyDictionary<string, PyObject> kwargs)
    {
        if (!TryParseExceptionGroupInfo(this, args, out var info, out var err))
            return err.Value;

        PyTypeObject type = cls;

        if (type.IsSubclassOf(PyExceptionGroupObjectType.Shared))
        {
            if (!info.Exceptions.All(PyExceptionObjectType.Shared.IsInstance))
            {
                if (type is PyExceptionGroupObjectType)
                    return PyResult.TypeError(PySR.Runtime_ExceptionGroup_NestBaseExceptionsForExceptionGroup);

                return PyResult.TypeError(PySR.Runtime_ExceptionGroup_NestBaseExceptions, type.TpName);
            }
        }

        if (type is PyBaseExceptionGroupObjectType)
        {
            if (info.Exceptions.All(static exc => PyExceptionObjectType.Shared.IsInstance(exc)))
                type = PyExceptionGroupObjectType.Shared;
        }

        return new PyExceptionObject(type, args, info);
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

    [PyProperty("message")]
    private static PyResult Get_Message(PyCallContext context, PyExceptionObject self)
        // TODO: AsGroup
        => PyStrObject.FromString(self.AsGroup!.Message);

    [PyProperty("exceptions")]
    private static PyResult Get_Exceptions(PyCallContext context, PyExceptionObject self)
        // TODO: AsGroup
        => PyTupleObject.CreateTuple([.. self.AsGroup!.Exceptions]);

    [PyMethod("derive")]
    [PyFunctionParameters("excs", "/")]
    private static PyResult Derive(PyCallContext context, PyExceptionObject self, PyArguments arguments)
    {
        if (!self.IsGroup)
            return PyResult.TypeError(null);

        if (!TryParseExceptions(arguments[0], out var excs, out var err))
            return err.Value;

        var info = new ExceptionGroupInfo(self.AsGroup.Message, [.. excs]);
        var excResult = PyExceptionObject.Create(context, self.PyType,
            [PyStrObject.FromString(info.Message),
            PyListObject.CreateList(info.Exceptions)], info);
        if (excResult.IsError)
            return excResult;

        var result = excResult.Value;
        // A fresh group starts with no traceback: CPython's derive builds from
        // (msg, excs) alone, and the sub-groups that do inherit one are loaded
        // by the split()/settlement paths below (exceptiongroup_subset).
        result.Cause = self.Cause;
        result.Context = self.Context;

        Debug.Assert(result.IsGroup);
        if (result.AsGroup.Exceptions.All(PyExceptionObjectType.Shared.IsInstance))
            result._pyType = PyExceptionGroupObjectType.Shared;
        else
            result._pyType = Shared;

        return result;
    }

    [PyMethod("split")]
    [PyFunctionParameters("condition", "/")]
    private static PyResult Split(PyCallContext context, PyExceptionObject self, PyArguments arguments)
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
            foreach (var o in tuple)
            {
                if (o is not PyTypeObject t || !t.IsSubclassOf(PyBaseExceptionObjectType.Shared))
                    return PyResult.TypeError(PySR.Runtime_ExceptionGroup_SplitExpectedCondition);

                types.Add(t);
            }

            predicate = exc => PyBoolObject.FromBoolean(types.Any(type => type.IsInstance(exc)));
        }
        else if (IsCallable(conditionObj))
        {
            predicate = exc =>
            {
                var result = conditionObj.Call(context, [exc]);
                if (result.IsError)
                    return result.ExceptionResult;

                return PySpecialMethods.Bool(context, result.Value);
            };
        }
        else
        {
            return PyResult.TypeError(PySR.Runtime_ExceptionGroup_SplitExpectedCondition);
        }

        (var err, PyObject? match, PyObject? rest) = SplitImpl(self);
        if (err is not null)
            return err.Value;

        match ??= PyNoneObject.None;
        rest ??= PyNoneObject.None;

        return PyTupleObject.CreateTuple(match, rest);


        // get_matcher_type: a non-class value is usable only as a predicate
        static bool IsCallable(PyObject obj)
            => obj.PyType.Slots.Call is not null ||
               PyObject.TryLookupAttrInMro(obj.PyType, PySpecialNames.Interned.Call.Value, out _);

        (PyResult? Error, PyExceptionObject? MatchGroup, PyExceptionObject? RestGroup) SplitImpl(PyExceptionObject exceptionGroup)
        {
            // exceptiongroup_split_recursive tests the node itself before
            // descending: a condition that matches a group yields that group
            // rather than a derived subset of its children
            var nodeMatch = predicate(exceptionGroup);
            if (nodeMatch.IsError)
                return ReturnError(nodeMatch);

            if (nodeMatch.Value.BoolValue)
                return (null, exceptionGroup, null);

            if (!exceptionGroup.IsGroup)
                return (null, null, exceptionGroup);

            List<PyExceptionObject> match = [];
            List<PyExceptionObject> rest = [];

            foreach (var subException in exceptionGroup.AsGroup.Exceptions)
            {
                var (err, subMatch, subRest) = SplitImpl(subException);
                if (err is not null)
                    return ReturnError(err.Value);

                if (subMatch is not null)
                    match.Add(subMatch);

                if (subRest is not null)
                    rest.Add(subRest);
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
                    return result.ExceptionResult;

                if (result.Value is not PyExceptionObject exc || !exc.IsGroup || !Shared.IsInstance(result.Value))
                    return PyResult.TypeError(PySR.Runtime_ExceptionGroup_DeriveReturnNonGroup);

                // exceptiongroup_subset reloads the metadata the derived group
                // must share with its source, so a split()-ed part counts as
                // "the same exception" as the group it was carved from. derive
                // itself leaves the traceback unset (CPython does the same).
                exc.Traceback = exceptionGroup.Traceback;
                exc.TracebackThreadInfo = exceptionGroup.TracebackThreadInfo;

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
            PyListObject list => list,
            PyTupleObject tuple => tuple,
            _ => null
        };

        if (excs is null)
        {
            err = PyResult.TypeError(PySR.Runtime_ExceptionGroup_NewGroup_ExcsNonSeq);
            return false;
        }

        if (excs.Count is 0)
        {
            err = PyResult.ValueError(PySR.Runtime_ExceptionGroup_NewGroup_ExcsEmpty);
            return false;
        }

        for (var i = 0; i < excs.Count; i++)
        {
            if (excs[i] is not PyExceptionObject)
            {
                err = PyResult.ValueError(PySR.Runtime_ExceptionGroup_NewGroup_ExcsItemNonExc, i);
                return false;
            }
        }

        exceptions = excs.Cast<PyExceptionObject>();
        err = null;
        return true;
    }

    // CPython BaseExceptionGroup_new parses only the args tuple and ignores
    // keyword arguments: the rejection lives in __init__, or a subclass with
    // a custom __init__ consumes the kwargs
    internal static bool TryParseExceptionGroupInfo(PyTypeObject exceptionGroupType, IReadOnlyList<PyObject> args,
        [NotNullWhen(true)] out ExceptionGroupInfo? info, [NotNullWhen(false)] out PyResult? err)
    {
        info = null;

        if (!PyArgsValidator.ValidateArgs(args, 2, out err))
            return false;

        if (args[0] is not PyStrObject msg)
        {
            err = PyResult.TypeError(PySR.Runtime_ExceptionGroup_NewGroup_MsgNonStr, exceptionGroupType.TpName, args[0].PyType.TpName);
            return false;
        }

        if (!TryParseExceptions(args[1], out var excs, out err))
            return false;

        info = new ExceptionGroupInfo(msg.Value, [.. excs]);
        return true;
    }
}

[PyException("ExceptionGroup", Bases = [typeof(PyBaseExceptionGroupObjectType), typeof(PyExceptionObjectType)])]
public sealed partial class PyExceptionGroupObjectType : PyExceptionType;

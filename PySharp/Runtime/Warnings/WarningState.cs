using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Text.RegularExpressions;

namespace PySharp.Runtime;

// The action taken when a warning is emitted.
internal enum WarningAction
{
    Default,
    Error,
    Ignore,
    Always,
    All = Always,   // "all" is an alias for "always"
    Module,
    Once,
}

// A filter mirrors CPython's 5-tuple (action, message_pattern, category, module_pattern, lineno).
// The message/module patterns are compiled lazily with .NET Regex; a null pattern matches anything,
// and a lineno of 0 matches any line. Order matters: the first matching entry wins.
internal readonly record struct WarningFilter(
    WarningAction Action,
    PyTypeObject<PyExceptionObject> Category,
    string? MessagePattern,
    string? ModulePattern,
    int Lineno);

internal sealed class WarningStateSnapshot
{
    internal required List<WarningFilter> Filters { get; init; }
    internal required WarningAction DefaultAction { get; init; }
    internal required PyDictObject WarnedOnce { get; init; }
    internal required PyListObject? RecordSink { get; init; }
}

// Per-interpreter warning policy: a filter list, a default action, and a version counter
// that invalidates accumulated deduplication entries when the policy changes.
internal sealed class WarningState
{
    private readonly List<WarningFilter> _filters = [];
    private PyDictObject _warnedOnce = new();
    private int _filtersVersion;
    private int _observedVersion;

    internal WarningAction DefaultAction { get; private set; } = WarningAction.Default;

    internal IReadOnlyList<WarningFilter> Filters => _filters;
    internal PyListObject? RecordSink { get; private set; }
    internal int FiltersVersion => _filtersVersion;

    internal WarningStateSnapshot Capture()
    {
        SyncVersion();
        return new WarningStateSnapshot
        {
            Filters = [.. _filters],
            DefaultAction = DefaultAction,
            WarnedOnce = new PyDictObject(_warnedOnce),
            RecordSink = RecordSink,
        };
    }

    internal void Restore(WarningStateSnapshot snapshot)
    {
        _filters.Clear();
        _filters.AddRange(snapshot.Filters);
        DefaultAction = snapshot.DefaultAction;

        _warnedOnce = new PyDictObject(snapshot.WarnedOnce);
        RecordSink = snapshot.RecordSink;

        _filtersVersion++;
        _observedVersion = _filtersVersion;
    }

    internal void SetRecordSink(PyListObject? recordSink)
        => RecordSink = recordSink;

    internal PyResult PrepareRegistry(PyDictObject registry)
    {
        var versionResult = registry.GetItem("version");
        if (versionResult.IsError && !versionResult.IsKeyError)
            return versionResult.ExceptionResult;

        if (versionResult.IsError
            || versionResult.Value is not PyIntObject version
            || version.Int32Value != FiltersVersion)
        {
            registry.Clear();
            registry.SetItem("version", PyIntObject.FromInteger(FiltersVersion));
        }

        return PyNoneObject.None;
    }

    internal PyResult ShouldSuppress(
        PyCallContext context,
        WarningAction action,
        string text,
        PyTypeObject<PyExceptionObject> category,
        int lineno,
        PyDictObject? registry)
    {
        SyncVersion();
        if (action is WarningAction.Always)
            return PyBoolObject.False;
        if (action is WarningAction.Once)
        {
            var dictionary = registry ?? _warnedOnce;
            var onceKey = CreateOnceKey(text, category);
            var onceResult = dictionary.GetItem(context, onceKey);
            if (onceResult.IsKeyError)
                return PyBoolObject.False;
            if (onceResult.IsError)
                return onceResult.ExceptionResult;
            var onceBoolResult = PySpecialMethods.Bool(context, onceResult.Value);
            if (onceBoolResult.IsError)
                return onceBoolResult.ExceptionResult;
            return onceBoolResult.Value;
        }
        if (registry is null)
            return PyBoolObject.False;

        var key = CreateRegistryKey(text, category, action is WarningAction.Module ? 0 : lineno);
        var result = registry.GetItem(context, key);
        if (result.IsKeyError)
            return PyBoolObject.False;
        if (result.IsError)
            return result.ExceptionResult;
        var boolResult = PySpecialMethods.Bool(context, result.Value);
        if (boolResult.IsError)
            return boolResult.ExceptionResult;
        return boolResult.Value;
    }

    internal PyResult MarkWarned(
        PyCallContext context,
        WarningAction action,
        string text,
        PyTypeObject<PyExceptionObject> category,
        int lineno,
        PyDictObject? registry)
    {
        SyncVersion();
        if (action is WarningAction.Once)
        {
            var onceKey = CreateOnceKey(text, category);
            return (registry ?? _warnedOnce).SetItem(context, onceKey, PyBoolObject.True);
        }
        if (action is WarningAction.Always)
            return PyNoneObject.None;
        if (registry is null)
            return PyNoneObject.None;

        return registry.SetItem(
            context,
            CreateRegistryKey(text, category, action is WarningAction.Module ? 0 : lineno),
            PyBoolObject.True);
    }

    private static PyTupleObject CreateRegistryKey(
        string text,
        PyTypeObject<PyExceptionObject> category,
        int lineno)
    {
        return PyTupleObject.CreateTuple(
            PyStrObject.FromString(text),
            category,
            PyIntObject.FromInteger(lineno));
    }

    private static PyTupleObject CreateOnceKey(
        string text,
        PyTypeObject<PyExceptionObject> category)
    {
        return PyTupleObject.CreateTuple(
            PyStrObject.FromString(text),
            category);
    }

    // Adds a filter that matches only by category (any module/message/lineno).
    internal void AddFilter(PyTypeObject<PyExceptionObject> category, WarningAction action)
        => AddFilter(new WarningFilter(action, category, null, null, 0));

    // Mirrors CPython's _add_filter: when append is false an equal filter is removed first and the
    // new entry is inserted at the front (highest precedence); when append is true it is only added
    // if not already present.
    internal void AddFilter(WarningFilter filter, bool append = false)
    {
        if (append)
        {
            if (!_filters.Contains(filter))
                _filters.Add(filter);
        }
        else
        {
            _filters.RemoveAll(existing => existing == filter);
            _filters.Insert(0, filter);
        }

        _filtersVersion++;
    }

    internal void ClearFilters()
    {
        _filters.Clear();
        _filtersVersion++;
    }

    internal void SetDefaultAction(WarningAction action)
    {
        DefaultAction = action;
        _filtersVersion++;
    }

    // Resolves the action for a warning: the first filter whose category is a supertype of the
    // given category and whose message/module/lineno constraints are satisfied wins, otherwise the
    // default action is used.
    internal WarningAction ResolveAction(PyTypeObject<PyExceptionObject> category, string text, string module, int lineno)
    {
        foreach (var filter in _filters)
        {
            if (Matches(filter, category, text, module, lineno))
                return filter.Action;
        }

        return DefaultAction;
    }

    private static bool Matches(WarningFilter filter, PyTypeObject<PyExceptionObject> category, string text, string module, int lineno)
    {
        if (!category.IsSubclassOf(filter.Category))
            return false;

        if (filter.Lineno is not 0 && filter.Lineno != lineno)
            return false;

        if (filter.MessagePattern is not null && !MatchAtStart(filter.MessagePattern, text, RegexOptions.IgnoreCase))
            return false;

        if (filter.ModulePattern is not null && !MatchAtStart(filter.ModulePattern, module, RegexOptions.None))
            return false;

        return true;
    }

    // Python's re.Pattern.match() is anchored at the start, so require the match to begin at index 0.
    private static bool MatchAtStart(string pattern, string input, RegexOptions options)
    {
        var match = new Regex(pattern, options).Match(input);
        return match.Success && match.Index is 0;
    }

    // When the filter policy changes, previously recorded warnings are forgotten so that a
    // changed policy is re-evaluated on the next emission.
    private void SyncVersion()
    {
        if (_observedVersion != _filtersVersion)
        {
            _warnedOnce.Clear();
            _observedVersion = _filtersVersion;
        }
    }
}

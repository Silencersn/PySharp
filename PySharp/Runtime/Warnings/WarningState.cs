using PySharp.Modules.Builtins;
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
    internal required HashSet<(string Module, string Text, PyTypeObject<PyExceptionObject> Category, int Lineno)> WarnedDefault { get; init; }
    internal required HashSet<(string Module, string Text, PyTypeObject<PyExceptionObject> Category)> WarnedModule { get; init; }
    internal required HashSet<(string Text, PyTypeObject<PyExceptionObject> Category)> WarnedOnce { get; init; }
}

// Per-interpreter warning policy: a filter list, a default action, and a version counter
// that invalidates accumulated deduplication entries when the policy changes.
internal sealed class WarningState
{
    private readonly List<WarningFilter> _filters = [];
    private readonly HashSet<(string Module, string Text, PyTypeObject<PyExceptionObject> Category, int Lineno)> _warnedDefault = [];
    private readonly HashSet<(string Module, string Text, PyTypeObject<PyExceptionObject> Category)> _warnedModule = [];
    private readonly HashSet<(string Text, PyTypeObject<PyExceptionObject> Category)> _warnedOnce = [];
    private int _filtersVersion;
    private int _observedVersion;

    internal WarningAction DefaultAction { get; private set; } = WarningAction.Default;

    internal IReadOnlyList<WarningFilter> Filters => _filters;

    internal WarningStateSnapshot Capture()
    {
        SyncVersion();
        return new WarningStateSnapshot
        {
            Filters = [.. _filters],
            DefaultAction = DefaultAction,
            WarnedDefault = [.. _warnedDefault],
            WarnedModule = [.. _warnedModule],
            WarnedOnce = [.. _warnedOnce],
        };
    }

    internal void Restore(WarningStateSnapshot snapshot)
    {
        _filters.Clear();
        _filters.AddRange(snapshot.Filters);
        DefaultAction = snapshot.DefaultAction;

        _warnedDefault.Clear();
        _warnedDefault.UnionWith(snapshot.WarnedDefault);
        _warnedModule.Clear();
        _warnedModule.UnionWith(snapshot.WarnedModule);
        _warnedOnce.Clear();
        _warnedOnce.UnionWith(snapshot.WarnedOnce);

        _filtersVersion++;
        _observedVersion = _filtersVersion;
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

    // Returns true when this warning was already emitted under the current filter version
    // and should therefore be suppressed. The deduplication key depends on the action:
    // "default" once per site, "module" once per module, "once" globally.
    internal bool ShouldSuppress(WarningAction action, string module, string text, PyTypeObject<PyExceptionObject> category, int lineno)
    {
        SyncVersion();
        return action switch
        {
            WarningAction.Module => _warnedModule.Contains((module, text, category)),
            WarningAction.Once => _warnedOnce.Contains((text, category)),
            _ => _warnedDefault.Contains((module, text, category, lineno)),
        };
    }

    internal void MarkWarned(WarningAction action, string module, string text, PyTypeObject<PyExceptionObject> category, int lineno)
    {
        SyncVersion();
        switch (action)
        {
            case WarningAction.Module:
                _warnedModule.Add((module, text, category));
                break;
            case WarningAction.Once:
                _warnedOnce.Add((text, category));
                break;
            default:
                _warnedDefault.Add((module, text, category, lineno));
                break;
        }
    }

    // When the filter policy changes, previously recorded warnings are forgotten so that a
    // changed policy is re-evaluated on the next emission.
    private void SyncVersion()
    {
        if (_observedVersion != _filtersVersion)
        {
            _warnedDefault.Clear();
            _warnedModule.Clear();
            _warnedOnce.Clear();
            _observedVersion = _filtersVersion;
        }
    }
}

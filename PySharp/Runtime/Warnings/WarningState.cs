using PySharp.Modules.Builtins;

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

// Matching is by category only: a warning whose category is a subclass of the entry's
// category is considered a match. Order matters, the first matching entry wins.
internal readonly record struct WarningFilter(PyTypeObject<PyExceptionObject> Category, WarningAction Action);

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

    internal void AddFilter(PyTypeObject<PyExceptionObject> category, WarningAction action)
    {
        _filters.Add(new WarningFilter(category, action));
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

    // Resolves the action for a warning category: the first filter whose category is a
    // supertype of the given category wins, otherwise the default action is used.
    internal WarningAction ResolveAction(PyTypeObject<PyExceptionObject> category)
    {
        foreach (var filter in _filters)
        {
            if (category.IsSubclassOf(filter.Category))
                return filter.Action;
        }

        return DefaultAction;
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

using PySharp.Modules.Builtins;

namespace PySharp.Runtime;

// The action taken when a warning is emitted.
internal enum WarningAction
{
    Default,
    Error,
    Ignore,
}

// Matching is by category only: a warning whose category is a subclass of the entry's
// category is considered a match. Order matters, the first matching entry wins.
internal readonly record struct WarningFilter(PyExceptionType Category, WarningAction Action);

// Deduplication key for the "default" action: a warning is shown once per
// (module, text, category, lineno) site.
internal readonly record struct WarningRegistryKey(string Module, string Text, PyExceptionType Category, int Lineno);

// Per-interpreter warning policy: a filter list, a default action, and a version counter
// that invalidates accumulated deduplication entries when the policy changes.
internal sealed class WarningState
{
    private readonly List<WarningFilter> _filters = [];
    private readonly HashSet<WarningRegistryKey> _warned = [];
    private int _filtersVersion;
    private int _observedVersion;

    internal WarningAction DefaultAction { get; private set; } = WarningAction.Default;

    internal IReadOnlyList<WarningFilter> Filters => _filters;

    internal void AddFilter(PyExceptionType category, WarningAction action)
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
    internal WarningAction ResolveAction(PyExceptionType category)
    {
        foreach (var filter in _filters)
        {
            if (category.IsSubclassOf(filter.Category))
                return filter.Action;
        }

        return DefaultAction;
    }

    // Returns true when this warning was already emitted under the current filter version
    // and should therefore be suppressed.
    internal bool ShouldSuppress(string module, string text, PyExceptionType category, int lineno)
    {
        SyncVersion();
        return _warned.Contains(new WarningRegistryKey(module, text, category, lineno));
    }

    internal void MarkWarned(string module, string text, PyExceptionType category, int lineno)
    {
        SyncVersion();
        _warned.Add(new WarningRegistryKey(module, text, category, lineno));
    }

    // When the filter policy changes, previously recorded warnings are forgotten so that a
    // changed policy is re-evaluated on the next emission.
    private void SyncVersion()
    {
        if (_observedVersion != _filtersVersion)
        {
            _warned.Clear();
            _observedVersion = _filtersVersion;
        }
    }
}

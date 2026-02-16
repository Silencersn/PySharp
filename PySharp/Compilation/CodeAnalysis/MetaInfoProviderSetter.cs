using PySharp.Runtime;

namespace PySharp.Compilation.CodeAnalysis;

internal readonly ref struct MetaInfoProviderSetter : IDisposable
{
    private readonly PyFrame _frame;
    private readonly ICodeMetaInfoProvider? _previous;

    public MetaInfoProviderSetter(PyFrame frame, ICodeMetaInfoProvider provider)
    {
        _frame = frame;
        _previous = _frame.MetaInfoProvider;
        _frame.MetaInfoProvider = provider;
    }

    void IDisposable.Dispose()
    {
        _frame?.MetaInfoProvider = _previous;
    }
}

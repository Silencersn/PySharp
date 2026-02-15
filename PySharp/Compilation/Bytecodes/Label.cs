namespace PySharp.Compilation.Bytecodes;

internal readonly struct Label
{
    private readonly int _id;
    public readonly int Id => _id;

    public Label(int id)
    {
        _id = id;
    }
}
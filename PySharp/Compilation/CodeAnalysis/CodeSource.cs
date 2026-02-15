namespace PySharp.Compilation.CodeAnalysis;

public sealed class CodeSource
{
    public string Name { get; }
    public CodeText Code { get; }

    public CodeSource(string name, string code)
    {
        Name = name;
        Code = new CodeText(code);
    }
}

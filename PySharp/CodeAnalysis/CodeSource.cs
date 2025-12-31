using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.CodeAnalysis;

public sealed class CodeSource
{
    public string Name { get; }
    public CodeText Code { get; }

    public CodeSource(string name, CodeText code)
    {
        Name = name;
        Code = code;
    }
}

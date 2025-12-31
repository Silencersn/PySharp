using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.CodeAnalysis;

public readonly record struct CodeTextPosition
{
    public readonly int Line;
    public readonly int Offset;

    public CodeTextPosition(int line, int offset)
    {
        Line = line;
        Offset = offset;
    }
}

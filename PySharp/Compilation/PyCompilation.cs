using PySharp.AstNodes;
using PySharp.PyRuntime.Calls;
using System;
using System.Collections.Generic;
using System.Text;

namespace PySharp.Compilation;

internal abstract class PyCompilation
{
    public abstract void Execute(PyCallContext context);
}

internal sealed class PyAstCompilation : PyCompilation
{
    private readonly AstModNode _astMod;

    public PyAstCompilation(AstModNode astMod)
    {
        _astMod = astMod;
    }

    public override void Execute(PyCallContext context)
    {
        _astMod.Execute(context, context.CurrentFrame);
    }
}

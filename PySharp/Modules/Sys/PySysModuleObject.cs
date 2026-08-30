using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;

namespace PySharp.Modules.Sys;

public class PySysModuleObject : PyModuleObject
{
    public override string? Origin => "built-in";

    public PySysModuleObject() : base("sys")
    {
    }

    public override void OnImport(PyCallContext context, PyEnvironment environment)
    {
        // CPython: interactive/embedded interpreters have sys.argv = ['']
        var args = environment.Args.Count > 0
            ? PyListObject.CreateList(environment.Args.Select(PyStrObject.FromString))
            : PyListObject.CreateList(PyStrObject.Empty);
        AppendAttribute("argv", args);

        // Wrap the environment's standard streams.
        AppendAttribute("stdin", PyStdIoObject.CreateInput(environment.In, "<stdin>"));
        AppendAttribute("stdout", PyStdIoObject.CreateOutput(environment.Out, "<stdout>"));
        AppendAttribute("stderr", PyStdIoObject.CreateOutput(environment.Error, "<stderr>"));
    }
}

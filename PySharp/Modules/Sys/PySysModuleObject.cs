using PySharp.Modules.Builtins;
using PySharp.Modules.IO;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Sys;

[PyModuleInclude(PyModuleIncludeScheme.StaticMembers, typeof(PySysFunctions))]
public partial class PySysModuleObject : PyModuleObject
{
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

        // The standard streams are TextIOWrapper instances over the host's raw
        // streams, built the way CPython's create_stdio does: universal
        // newlines, the environment's encoding, and the per-stream error
        // handler (stderr uses backslashreplace). The process's handles stay
        // owned by the host, so close() only closes the wrapper.
        AppendAttribute("stdin", PyTextIOWrapperObject.CreateStandardStream(
            environment.InStream, "<stdin>", "r",
            readable: true, writable: false,
            environment.StdInEncoding, "strict"));
        AppendAttribute("stdout", PyTextIOWrapperObject.CreateStandardStream(
            environment.OutStream, "<stdout>", "w",
            readable: false, writable: true,
            environment.StdOutEncoding, "strict"));
        AppendAttribute("stderr", PyTextIOWrapperObject.CreateStandardStream(
            environment.ErrorStream, "<stderr>", "w",
            readable: false, writable: true,
            environment.StdErrEncoding, "backslashreplace"));
    }
}

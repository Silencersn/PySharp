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

        // sys.path is the live module search path: the import machinery reads
        // this list object on every lookup, so sys.path.append/insert from
        // Python affects the next import (CPython's path list in sysmodule.c)
        AppendAttribute("path", PyListObject.CreateList(environment.Paths.Select(PyStrObject.FromString)));

        // sys.modules is the import registry itself (CPython interp->modules):
        // the machinery reads it before the finder chain and registers every
        // loaded module in it, so deletions force a reload and assignments
        // substitute the cached module
        AppendAttribute("modules", PyDictObject.CreateDict(
            environment.Modules.Where(pair => pair.Value is not null)
                .Select(pair => KeyValuePair.Create(pair.Key, (PyObject)pair.Value!))));

        // int.to_bytes/from_bytes document passing sys.byteorder as the order
        AppendAttribute("byteorder", PyStrObject.FromString(BitConverter.IsLittleEndian ? "little" : "big"));

        // The standard streams are TextIOWrapper instances over the host's raw
        // streams, built the way CPython's create_stdio does: universal
        // newlines, the environment's encoding, and the per-stream error
        // handler (stderr uses backslashreplace). Buffering follows
        // create_stdio too: stdin/stdout line-buffered on a terminal and
        // block-buffered when redirected, stderr line-buffered whatever its
        // redirection. The process's handles stay owned by the host, so
        // close() only closes the wrapper.
        AppendAttribute("stdin", PyTextIOWrapperObject.CreateStandardStream(
            environment.InStream, "<stdin>", "r",
            readable: true, writable: false,
            environment.StdInEncoding, "strict",
            environment.StdInIsTerminal
                ? PyStandardStreamBuffering.Line
                : PyStandardStreamBuffering.Block,
            environment.StdInIsTerminal));
        AppendAttribute("stdout", PyTextIOWrapperObject.CreateStandardStream(
            environment.OutStream, "<stdout>", "w",
            readable: false, writable: true,
            environment.StdOutEncoding, "strict",
            environment.StdOutIsTerminal
                ? PyStandardStreamBuffering.Line
                : PyStandardStreamBuffering.Block,
            environment.StdOutIsTerminal));
        AppendAttribute("stderr", PyTextIOWrapperObject.CreateStandardStream(
            environment.ErrorStream, "<stderr>", "w",
            readable: false, writable: true,
            environment.StdErrEncoding, "backslashreplace",
            PyStandardStreamBuffering.Line,
            environment.StdErrIsTerminal));
    }
}

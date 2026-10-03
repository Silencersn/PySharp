using PySharp.Modules.Builtins;
using PySharp.Modules.IO;
using PySharp.Runtime;
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

        // CPython Py_GetPlatform: "win32" on Windows, "darwin" on macOS,
        // "linux" elsewhere
        AppendAttribute("platform", PyStrObject.FromString(
            OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : "linux"));

        // The interpreter binary and its installation root; an embedded
        // host without a process path reports the empty string
        AppendAttribute("executable", PyStrObject.FromString(Environment.ProcessPath ?? string.Empty));
        AppendAttribute("prefix", PyStrObject.FromString(AppContext.BaseDirectory));

        // Py_ssize_t's upper bound — the largest sequence length a 64-bit
        // build can address (sysmodule.c PY_SSIZE_T_MAX)
        AppendAttribute("maxsize", PyIntObject.FromInteger(long.MaxValue));
        // The largest code point: 0x10FFFF
        AppendAttribute("maxunicode", PyIntObject.FromInteger(0x10FFFF));

        // PySharp never writes bytecode caches — the permanent -B behavior
        AppendAttribute("dont_write_bytecode", PyBoolObject.True);

        // The interpreter's statically-linked module set — PyStandardLibrary's
        // switch keys, kept sorted like CPython's list_builtin_module_names
        AppendAttribute("builtin_module_names", PyTupleObject.CreateTuple(
            PyStandardLibrary.BuiltinModuleNames.Select(PyStrObject.FromString)));

        // The language version this interpreter implements (CPython
        // Py_GetVersion starts the line with it)
        AppendAttribute("version", PyStrObject.FromString("3.14.4 (PySharp) [64-bit]"));
        AppendAttribute("version_info", PyVersionInfoObject.Shared);

        // sys.implementation rides a SimpleNamespace, the way CPython 3.14
        // ships it (make_impl_info)
        var implementation = new PySimpleNamespaceObject();
        implementation.PyAttributes["name"] = PyStrObject.FromString("cpython");
        implementation.PyAttributes["cache_tag"] = PyStrObject.FromString("cpython-314");
        implementation.PyAttributes["version"] = PyVersionInfoObject.Shared;
        // (3 << 24) | (14 << 16) | (4 << 8) | 0xF0 — 3.14.4 final
        implementation.PyAttributes["hexversion"] = PyIntObject.FromInteger(0x30E04F0);
        implementation.PyAttributes["supports_isolated_interpreters"] = PyBoolObject.True;
        AppendAttribute("implementation", implementation);

        AppendAttribute("flags", PyFlagsObject.Create());

        // CPython copies the built-in hook into sys.__excepthook__ so a
        // replaced sys.excepthook keeps the default reachable
        AppendAttribute("__excepthook__", PySysFunctions.Excepthook);

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

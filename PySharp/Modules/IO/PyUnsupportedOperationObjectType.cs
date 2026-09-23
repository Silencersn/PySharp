using PySharp.Modules.Builtins;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.IO;

// CPython Modules/_io/_iomodule.c: UnsupportedOperation inherits from BOTH
// OSError and ValueError, and lives in the _io/io module namespace — never
// in builtins. PySharp has no io module yet, so the type carries
// __module__ = "_io" but is intentionally not importable; raised only from
// the runtime's own io paths (e.g. text-mode nonzero relative seek) via the
// generic PyResult.RaiseException — no per-type throw factory, that
// machinery is reserved for the builtins exception table.
[PyException("UnsupportedOperation", Module = "_io", Bases = [typeof(PyOSErrorObjectType), typeof(PyValueErrorObjectType)])]
public sealed partial class PyUnsupportedOperationObjectType : PyExceptionType;

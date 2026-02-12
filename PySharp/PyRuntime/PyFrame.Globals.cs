using PySharp.PyModules.Builtins;
using PySharp.Utility;
using System.Collections.Concurrent;

namespace PySharp.PyRuntime;

partial class PyFrame
{
    internal sealed class PyFrameGlobals
    {
        private readonly IDictionary<string, PyObject> _globals;
        private PyDictObject? _pyDict;

        public PyFrameGlobals()
        {
            _globals = new ConcurrentDictionary<string, PyObject>();
        }
        public PyFrameGlobals(ConcurrentDictionary<string, PyObject> globals)
        {
            _globals = globals;
        }

        public IDictionary<string, PyObject> Globals => _globals;
        public PyDictObject PyDict => _pyDict ??= PyDictObject.CreateProxy(new DictAdapter(Globals!));

        public PyFrameGlobals Clone()
        {
            return new PyFrameGlobals(new(_globals));
        }
    }
}

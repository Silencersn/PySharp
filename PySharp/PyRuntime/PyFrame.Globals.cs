using PySharp.PyModules.Builtins;
using PySharp.Utility;
using System.Collections.Concurrent;

namespace PySharp.PyRuntime;

partial class PyFrame
{
    internal sealed class PyFrameGlobals
    {
        private readonly ConcurrentDictionary<string, PyObject> _globals;
        private DictAdapter? _globalsAdapter;
        private PyDictObject? _pyDict;

        public PyFrameGlobals()
        {
            _globals = [];
        }
        public PyFrameGlobals(ConcurrentDictionary<string, PyObject> globals)
        {
            _globals = globals;
        }

        public ConcurrentDictionary<string, PyObject> Globals => _globals;
        public DictAdapter GlobalsAdapter => _globalsAdapter ??= new DictAdapter(Globals!);
        public PyDictObject PyDict => _pyDict ??= PyDictObject.CreateProxy(GlobalsAdapter);

        public PyFrameGlobals Clone()
        {
            return new PyFrameGlobals(new(_globals));
        }
    }
}

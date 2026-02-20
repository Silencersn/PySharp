using PySharp.Modules.Builtins;
using PySharp.Utility;
using System.Collections.Concurrent;

namespace PySharp.Runtime;

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
        public PyFrameGlobals(ConcurrentDictionary<string, PyObject> globals, PyDictObject? dict = null)
        {
            _globals = globals;
            _pyDict = dict;
        }
        public PyFrameGlobals(PyDictObject dict)
        {
            _pyDict = dict;
            _globals = new StringKeyDict(_pyDict);
        }

        public IDictionary<string, PyObject> Globals => _globals;
        public PyDictObject PyDict => _pyDict ??= PyDictObject.CreateProxy(new DictAdapter(Globals!));

        public PyFrameGlobals Clone()
        {
            if (_pyDict is null)
                return new PyFrameGlobals(new ConcurrentDictionary<string, PyObject>(_globals));

            var dict = PyDictObject.CreateDict(_pyDict);
            return new PyFrameGlobals(dict);
        }
    }
}

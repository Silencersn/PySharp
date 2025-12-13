using PySharp.PyModules.Builtins;
using PySharp.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

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
            _globalsAdapter = null;
        }

        public ConcurrentDictionary<string, PyObject> Globals => _globals;
        public DictAdapter GlobalsAdapter => _globalsAdapter ??= new DictAdapter(Globals!);
        public PyDictObject PyDict => _pyDict ??= PyDictObject.CreateProxy(GlobalsAdapter);
    }
}

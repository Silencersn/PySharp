using PySharp.Modules.Builtins;
using PySharp.Utility;

namespace PySharp.Runtime;

internal sealed class PyGlobals
{
    private readonly IDictionary<string, PyObject> _dict;
    private PyDictObject? _pyDict;

    internal PyGlobals(IDictionary<string, PyObject> dict)
    {
        _dict = dict;
    }
    internal PyGlobals(PyDictObject pyDict)
    {
        _pyDict = pyDict;
        _dict = new StringKeyDict(pyDict);
    }

    public IDictionary<string, PyObject> Dict => _dict;
    public PyDictObject PyDict => _pyDict ??= PyDictObject.CreateProxy(new DictAdapter(Dict!));

    public PyGlobals Clone()
    {
        if (_pyDict is null)
            return new PyGlobals(new Dictionary<string, PyObject>(_dict));

        return new PyGlobals(PyDictObject.CreateDict(_pyDict));
    }
}

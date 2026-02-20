using PySharp.Runtime;
using PySharp.Runtime.Calls;
using System.Text;

namespace PySharp.Modules.Builtins;

partial class PyStrObject
{
    public PyResult PyJoin(PyCallContext context, PyObject iterable)
    {
        var list = PyUtils.IterableToList(context, iterable);
        if (list.IsError)
            return list;

        var builder = new StringBuilder();
        int index = 0;
        foreach (var item in list.Value)
        {
            if (item is not PyStrObject strObj)
                return PyResult.TypeError(PySR.Runtime_String_JoinNonStrAt, index, item.PyType.FullName);

            if (index > 0)
                builder.Append(Value);
            builder.Append(strObj.Value);
            index++;
        }

        return FromString(builder.ToString());
    }
}

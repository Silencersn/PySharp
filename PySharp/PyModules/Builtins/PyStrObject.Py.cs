using PySharp.PyRuntime;
using System.Text;

namespace PySharp.PyModules.Builtins;

partial class PyStrObject
{
    public PyObject? PyJoin(PyObject iterable)
    {
        var items = Utils.EnumerableIterable(iterable);
        if (items is null)
            return null;

        var builder = new StringBuilder();
        int index = 0;
        foreach (var item in items)
        {
            if (item is null)
                return null;

            if (item is not PyStrObject strObj)
                return PyVirtualMachine.RaiseTypeError($"sequence item {index}: expected str instance, {item.PyType.Name} found");

            if (index > 0)
                builder.Append(Value);
            builder.Append(strObj.Value);
            index++;
        }

        return FromString(builder.ToString());
    }
}

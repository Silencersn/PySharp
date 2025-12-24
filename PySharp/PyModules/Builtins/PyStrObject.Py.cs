using PySharp.PyRuntime.Calls;
using System.Text;

namespace PySharp.PyModules.Builtins;

partial class PyStrObject
{
    public PyResult PyJoin(PyCallContext context, PyObject iterable)
    {
        if (!Utils.TryEnumeratedIterable(context, iterable, out var items, out var err))
            return err.Value;

        var builder = new StringBuilder();
        int index = 0;
        foreach (var item in items)
        {
            if (item is not PyStrObject strObj)
                return PyResult.RaiseTypeError($"sequence item {index}: expected str instance, {item.PyType.Name} found");

            if (index > 0)
                builder.Append(Value);
            builder.Append(strObj.Value);
            index++;
        }

        return FromString(builder.ToString());
    }
}

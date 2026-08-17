using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.Site;

public static partial class PySiteFunctions
{
    [PyExport("exit", nameof(ExitImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Exit { get; }
    public static PyBuiltinFunctionOrMethodObject Quit => Exit;
    [PyExport("help", nameof(HelpImpl_1), nameof(HelpImpl_2))]
    public static partial PyBuiltinFunctionOrMethodObject Help { get; }

    [PyFunctionParameters("code=None")]
    private static PyResult ExitImpl(PyCallContext context, PyArguments arguments)
    {
        int? exitCode = arguments[0] switch
        {
            PyIntObject intObj => intObj.Int32Value,
            PyNoneObject => 0,
            _ => null
        };

        if (!exitCode.HasValue)
            return PyResult.TypeError(null);

        context.Exit(exitCode.Value);
        return PyNoneObject.None;
    }

    [PyFunctionParameters()]
    private static PyResult HelpImpl_1(PyCallContext context, PyArguments arguments)
    {
        context.Out.Write(
            """
            Welcome to PySharp help!

            Usage:
              help()            - show this message
              help(object)      - show help on an object
              help('name')      - look up a name in builtins
            """);
        return PyNoneObject.None;
    }

    [PyFunctionParameters("request", "/")]
    private static PyResult HelpImpl_2(PyCallContext context, PyArguments arguments)
    {
        var request = arguments[0];
        if (request is PyStrObject nameObj)
        {
            var builtins = context.PyEnvironment.LoadBuiltinModule(context, "builtins");
            if (builtins.PyAttributes.TryGetValue(nameObj.Value, out var target))
                return PrintHelp(context, target);

            context.Out.WriteLine($"No Python documentation found for '{nameObj.Value}'.");
            return PyNoneObject.None;
        }

        return PrintHelp(context, request);
    }

    private static PyResult PrintHelp(PyCallContext context, PyObject obj)
    {
        var heading = obj.PyType.FullName;
        if (obj.PyAttributes.TryGetValue(PySpecialNames.Name, out var nameObj) && nameObj is PyStrObject nameStr)
            heading += $" {nameStr.Value}";
        context.Out.WriteLine($"Help on {heading}:");
        if (obj.PyAttributes.TryGetValue(PySpecialNames.Doc, out var doc) &&
            doc is PyStrObject docStr && docStr.Value.Length > 0)
        {
            context.Out.WriteLine();
            context.Out.WriteLine(docStr.Value);
        }
        return PyNoneObject.None;
    }
}

using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.PyAttributes;
using System.Text.RegularExpressions;

namespace PySharp.Modules.Warnings;

public static partial class PyWarningsFunctions
{
    [PyExport("warn", nameof(WarnImpl))]
    public static partial PyBuiltinFunctionOrMethodObject Warn { get; }

    [PyExport("warn_explicit", nameof(WarnExplicitImpl))]
    public static partial PyBuiltinFunctionOrMethodObject WarnExplicit { get; }

    [PyFunctionParameters("message", "category=None", "stacklevel=1")]
    private static PyResult WarnImpl(PyCallContext context, PyArguments arguments)
    {
        var message = arguments[0];
        var categoryObj = arguments[1];

        PyTypeObject<PyExceptionObject>? category = null;
        // A Warning instance determines its own category, so the explicit category is only
        // validated when the message is not already a Warning instance (mirroring CPython).
        if (categoryObj is not PyNoneObject && !PyWarningObjectType.Shared.IsInstance(message))
        {
            category = categoryObj as PyTypeObject<PyExceptionObject>;
            if (category is null)
                return PyResult.TypeError($"category must be a Warning subclass, not '{categoryObj.PyType.Name}'");
        }

        var stacklevelResult = PySpecialMethods.Index(context, arguments[2]);
        if (stacklevelResult.IsError)
            return stacklevelResult.ExceptionResult;
        if (!stacklevelResult.Value.IsInt32)
            return PyResult.OverflowError("stacklevel is too large");

        return context.Warn(message, category, stacklevelResult.Value.Int32Value);
    }

    [PyFunctionParameters("message", "category", "filename", "lineno", "module=None", "registry=None", "module_globals=None", "source=None")]
    private static PyResult WarnExplicitImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[2] is not PyStrObject filenameObj)
            return PyResult.TypeError("filename must be a string");

        var message = arguments[0];
        // A Warning instance determines its own category, so the explicit category is only
        // validated when the message is not already a Warning instance (mirroring CPython).
        PyTypeObject<PyExceptionObject>? category;
        if (PyWarningObjectType.Shared.IsInstance(message))
        {
            category = message.PyType as PyTypeObject<PyExceptionObject>;
        }
        else
        {
            if (arguments[1] is not PyTypeObject<PyExceptionObject> cat || !cat.IsSubclassOf(PyWarningObjectType.Shared))
                return PyResult.TypeError($"category must be a Warning subclass, not '{arguments[1].PyType.Name}'");
            category = cat;
        }

        var linenoResult = PySpecialMethods.Index(context, arguments[3]);
        if (linenoResult.IsError)
            return linenoResult.ExceptionResult;
        if (!linenoResult.Value.IsInt32)
            return PyResult.OverflowError("lineno is too large");

        if (arguments[4] is not (PyNoneObject or PyStrObject))
            return PyResult.TypeError("module must be a string or None");
        if (arguments[5] is not (PyNoneObject or PyDictObject))
            return PyResult.TypeError("registry must be a dict or None");
        if (arguments[6] is not (PyNoneObject or PyDictObject))
            return PyResult.TypeError($"module_globals must be a dict, not '{arguments[6].PyType.Name}'");

        string? module = arguments[4] is PyStrObject moduleObj ? moduleObj.Value : null;
        var registry = arguments[5] as PyDictObject;
        // module_globals is validated above but otherwise unused, mirroring CPython where it is only
        // used to prime the linecache for formatting (no behavioral effect on dedup/emission).
        return context.WarnExplicit(
            message,
            category,
            filenameObj.Value,
            linenoResult.Value.Int32Value,
            module,
            registry,
            arguments[7]);
    }

    [PyExport("filterwarnings", nameof(FilterWarningsImpl))]
    public static partial PyBuiltinFunctionOrMethodObject FilterWarnings { get; }

    [PyFunctionParameters("action", "message=''", "category=None", "module=''", "lineno=0", "append=False")]
    private static PyResult FilterWarningsImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject actionObj)
            return PyResult.TypeError("action must be a string");
        if (!TryParseAction(actionObj.Value, out var action))
            return PyResult.ValueError($"invalid action: '{actionObj.Value}'");

        if (arguments[1] is not PyStrObject messageObj)
            return PyResult.TypeError("message must be a string");
        if (arguments[3] is not PyStrObject moduleObj)
            return PyResult.TypeError("module must be a string");
        if (arguments[4] is not PyIntObject linenoObj)
            return PyResult.TypeError("lineno must be an int");
        if (linenoObj.Int32Value < 0)
            return PyResult.ValueError("lineno must be an int >= 0");

        var category = ResolveCategory(arguments[2]);
        if (category is null)
            return PyResult.TypeError($"category must be a Warning subclass, not '{arguments[2].PyType.Name}'");

        var messagePattern = EmptyToNull(messageObj.Value);
        if (messagePattern is not null && !TryCompile(messagePattern, RegexOptions.IgnoreCase))
            return PyResult.PySharpException("NotImplemented");
        var modulePattern = EmptyToNull(moduleObj.Value);
        if (modulePattern is not null && !TryCompile(modulePattern, RegexOptions.None))
            return PyResult.PySharpException("NotImplemented");

        context.PyEnvironment.Warnings.AddFilter(
            new WarningFilter(action, category, messagePattern, modulePattern, linenoObj.Int32Value),
            AsBool(context, arguments[5]));

        return default;
    }

    [PyExport("simplefilter", nameof(SimpleFilterImpl))]
    public static partial PyBuiltinFunctionOrMethodObject SimpleFilter { get; }

    [PyFunctionParameters("action", "category=None", "lineno=0", "append=False")]
    private static PyResult SimpleFilterImpl(PyCallContext context, PyArguments arguments)
    {
        if (arguments[0] is not PyStrObject actionObj)
            return PyResult.TypeError("action must be a string");
        if (!TryParseAction(actionObj.Value, out var action))
            return PyResult.ValueError($"invalid action: '{actionObj.Value}'");
        if (arguments[2] is not PyIntObject linenoObj)
            return PyResult.TypeError("lineno must be an int");
        if (linenoObj.Int32Value < 0)
            return PyResult.ValueError("lineno must be an int >= 0");

        var category = ResolveCategory(arguments[1]);
        if (category is null)
            return PyResult.TypeError($"category must be a Warning subclass, not '{arguments[1].PyType.Name}'");

        context.PyEnvironment.Warnings.AddFilter(
            new WarningFilter(action, category, null, null, linenoObj.Int32Value),
            AsBool(context, arguments[3]));

        return default;
    }

    [PyExport("resetwarnings", nameof(ResetWarningsImpl))]
    public static partial PyBuiltinFunctionOrMethodObject ResetWarnings { get; }

    [PyExport("catch_warnings", nameof(CatchWarningsImpl))]
    public static partial PyBuiltinFunctionOrMethodObject CatchWarnings { get; }

    [PyFunctionParameters("record=False", "module=None", "action=None", "category=None", "lineno=0", "append=False")]
    private static PyResult CatchWarningsImpl(PyCallContext context, PyArguments arguments)
    {
        var record = AsBool(context, arguments[0]);
        if (arguments[1] is not PyNoneObject)
            return PyResult.PySharpException("NotImplemented");

        WarningAction? action = null;
        if (arguments[2] is not PyNoneObject)
        {
            if (arguments[2] is not PyStrObject actionObj)
                return PyResult.TypeError("action must be a string");
            if (!TryParseAction(actionObj.Value, out var parsedAction))
                return PyResult.ValueError($"invalid action: '{actionObj.Value}'");
            action = parsedAction;
        }

        var category = ResolveCategory(arguments[3]);
        if (category is null)
            return PyResult.TypeError($"category must be a Warning subclass, not '{arguments[3].PyType.Name}'");
        if (arguments[4] is not PyIntObject linenoObj)
            return PyResult.TypeError("lineno must be an int");
        if (linenoObj.Int32Value < 0)
            return PyResult.ValueError("lineno must be an int >= 0");

        return new PyCatchWarningsObject(
            action,
            category,
            linenoObj.Int32Value,
            AsBool(context, arguments[5]),
            record ? PyListObject.CreateList() : null);
    }

    [PyFunctionParameters()]
    private static PyResult ResetWarningsImpl(PyCallContext context, PyArguments arguments)
    {
        context.PyEnvironment.Warnings.ClearFilters();
        return default;
    }

    internal static bool TryParseAction(string action, out WarningAction result)
    {
        switch (action)
        {
            case "default": result = WarningAction.Default; return true;
            case "error": result = WarningAction.Error; return true;
            case "ignore": result = WarningAction.Ignore; return true;
            case "always": result = WarningAction.Always; return true;
            case "all": result = WarningAction.All; return true;
            case "module": result = WarningAction.Module; return true;
            case "once": result = WarningAction.Once; return true;
            default: result = default; return false;
        }
    }

    internal static PyTypeObject<PyExceptionObject>? ResolveCategory(PyObject categoryObj)
    {
        if (categoryObj is PyNoneObject)
            return PyWarningObjectType.Shared;
        if (categoryObj is PyTypeObject<PyExceptionObject> type && type.IsSubclassOf(PyWarningObjectType.Shared))
            return type;
        return null;
    }

    private static string? EmptyToNull(string value) => value.Length is 0 ? null : value;

    private static bool TryCompile(string pattern, RegexOptions options)
    {
        try
        {
            _ = new Regex(pattern, options);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static bool AsBool(PyCallContext context, PyObject value)
    {
        if (value is PyBoolObject boolObject)
            return boolObject.BoolValue;
        return PySpecialMethods.Bool(context, value).PyUnwrap(context).BoolValue;
    }
}

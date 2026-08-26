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

    // Emits a warning through the core warning machinery. The message is any object (its str()
    // is used), the category defaults to UserWarning when None, and stacklevel attributes the
    // warning to the caller's frame (1 = the line that invoked warnings.warn).
    [PyFunctionParameters("message", "category=None", "stacklevel=1")]
    private static PyResult WarnImpl(PyCallContext context, PyArguments arguments)
    {
        var message = arguments[0];
        var categoryObj = arguments[1];

        PyTypeObject<PyExceptionObject>? category = null;
        if (categoryObj is not PyNoneObject)
        {
            category = categoryObj as PyTypeObject<PyExceptionObject>;
            if (category is null)
                return PyResult.TypeError($"category must be a Warning subclass, not '{categoryObj.PyType.Name}'");
        }

        if (arguments[2] is not PyIntObject stacklevelObj)
            return PyResult.TypeError($"'{arguments[2].PyType.Name}' object cannot be interpreted as an integer");

        return context.Warn(message, category, stacklevelObj.Int32Value);
    }

    [PyExport("filterwarnings", nameof(FilterWarningsImpl))]
    public static partial PyBuiltinFunctionOrMethodObject FilterWarnings { get; }

    // Inserts a full filter entry (action + message/module regex + category + lineno) into the
    // warning filter list, mirroring CPython's warnings.filterwarnings. An empty message/module
    // matches anything; message is matched case-insensitively and module case-sensitively.
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

    // Adds a filter that matches any module/message, so only the category and lineno apply.
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

    // Clears the warning filter list entirely, so no filters are active.
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

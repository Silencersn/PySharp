using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime.Environments;

partial class PyEnvironment
{
    /// <summary>
    /// Resolves a relative import name to an absolute module name.
    /// Implements CPython's resolve_name algorithm (PEP 328).
    /// </summary>
    /// <param name="context">The call context.</param>
    /// <param name="packageObj">The __package__ value from the caller's globals (may be null, None, or a string).</param>
    /// <param name="moduleName">The __name__ value from the caller's globals.</param>
    /// <param name="hasPath">Whether __path__ exists in the caller's globals (indicating the caller is a package).</param>
    /// <param name="name">The module name from the import statement (may be empty).</param>
    /// <param name="level">The relative import level (1 = current package, 2 = parent package, etc.). Must be > 0.</param>
    /// <returns>The resolved absolute module name.</returns>
    [AIGenerated]
    internal static string ResolveRelativeModuleName(PyCallContext context, PyObject? packageObj, string moduleName, bool hasPath, string name, int level)
    {
        Debug.Assert(level > 0);

        // Step 1: Determine the current package from globals
        string package;

        if (packageObj is PyStrObject packageStr)
        {
            package = packageStr.Value;
        }
        else if (packageObj is null or PyNoneObject)
        {
            // __package__ is None or absent: fallback to __name__ and __path__
            if (hasPath)
            {
                // Caller is a package; __package__ = __name__
                package = moduleName;
            }
            else
            {
                // Caller is a regular module; __package__ = parent package name
                var lastDot = moduleName.LastIndexOf('.');
                package = lastDot >= 0 ? moduleName[..lastDot] : string.Empty;
            }
        }
        else
        {
            // __package__ is set to a non-string value: raise TypeError
            throw context.TypeError(PySR.Runtime_Import_PackageNotString);
        }

        // Step 2: Validate package
        if (package.Length is 0)
            throw context.ImportError(PySR.Runtime_Import_RelativeNoKnownParentPackage);

        // Step 3: Walk up the package hierarchy based on level
        for (int i = 1; i < level; i++)
        {
            var lastDot = package.LastIndexOf('.');
            if (lastDot < 0)
                throw context.ImportError(PySR.Runtime_Import_RelativeBeyondTopLevel);
            package = package[..lastDot];
        }

        // Step 4: Concatenate with the (possibly empty) module name
        if (name.Length is 0)
            return package;
        return package + '.' + name;
    }
    internal PyModuleObject LoadBuiltinModule(PyCallContext context, string name)
    {
        if (Modules.TryGetValue(name, out var module))
        {
            Debug.Assert(module is not null);
            return module;
        }

        module = PyStandardLibrary.TryCreateModule(context, name);
        Debug.Assert(module is not null);
        module.OnImport(context, this);
        Modules.Add(name, module);
        return module;
    }

    internal bool TryLoadModule(PyCallContext context, string qualifiedName, [NotNullWhen(true)] out PyModuleObject? rootModule, [NotNullWhen(true)] out PyModuleObject? module)
    {
        return InternalTryLoadModule(context, qualifiedName, out rootModule, out module);
    }

    internal bool InternalTryLoadModule(PyCallContext context, string qualifiedName, [NotNullWhen(true)] out PyModuleObject? rootModule, [NotNullWhen(true)] out PyModuleObject? module)
    {
        if (!qualifiedName.Contains('.'))
        {
            var result = InternalTryLoadRootModule(context, qualifiedName, out rootModule);
            module = rootModule;
            return result;
        }

        module = null;
        var parts = qualifiedName.Split('.');
        if (!InternalTryLoadRootModule(context, parts[0], out rootModule))
            return false;

        module = rootModule;
        var preModule = rootModule;
        for (int i = 1; i < parts.Length; i++)
        {
            if (!preModule.PyAttributes.TryGetValue(PySpecialNames.Path, out var pyObj))
                return false;

            var list = PyUtils.IterableToList(context, pyObj);
            if (list.IsError)
                // TODO: throw exception directly?
                return false;

            var paths = new List<string>(list.Value.Count);
            foreach (var item in list.Value)
            {
                if (item is not PyStrObject str)
                    return false;

                paths.Add(str.Value);
            }

            var qualName = string.Join('.', parts[..(i + 1)]);
            if (!InternalTryLoadModule(context, paths, qualName, out module))
                return false;
            preModule.PyAttributes[parts[i]] = module;
            preModule = module;
        }

        return true;
    }

    internal bool InternalTryLoadRootModule(PyCallContext context, string name, [NotNullWhen(true)] out PyModuleObject? module)
    {
        return InternalTryLoadModule(context, Paths, name, out module);
    }

    internal bool InternalTryLoadModule(PyCallContext context, IReadOnlyList<string> paths, string qualifiedName, [NotNullWhen(true)] out PyModuleObject? module)
    {
        Debug.Assert(!qualifiedName.StartsWith('.'));

        if (Modules.TryGetValue(qualifiedName, out module))
            // the key may be assigned to None,
            // forcing the next import of the module to result in a ModuleNotFoundError
            return module is not null;

        var frame = PyInternalFrame.CreateModuleFrame(context, isRoot: false, qualifiedName);
        using var withFrame = context.WithFrame(ref frame);

        foreach (var provider in ModuleProviders)
        {
            if (!provider.TryGetModule(context, qualifiedName, paths, out module))
                continue;

            Modules.Add(qualifiedName, module);
            return true;
        }

        return false;
    }
}

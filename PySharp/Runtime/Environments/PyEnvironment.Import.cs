using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime.Environments;

// CPython's _find_and_load_unlocked fails a dotted import whose parent module
// imported successfully but exposes no __path__ with an explicit second
// clause — "No module named 'a.b'; 'a' is not a package" — and names the
// failing level's child on the exception: importing 'a.b.c' with a
// non-package 'a' reports 'a.b', because the error belongs to the parent
// chain's own import rather than to the originally requested name.
[AIGenerated]
internal readonly record struct ModuleNotPackageFailure(string ModuleName, string ParentName);

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
        if (TryGetRegisteredModule(context, name, out var cached))
        {
            Debug.Assert(cached is not null);
            return cached;
        }

        var module = PyStandardLibrary.TryCreateModule(context, name);
        Debug.Assert(module is not null);
        // Register before OnImport so a reentrant load during initialization
        // finds the module instead of recursing, same bargain as the
        // provider chain below.
        RegisterLoadedModule(name, module);
        module.OnImport(context, this);
        return module;
    }

    // sys.modules is the authoritative import cache once the sys module
    // exists — CPython's import machinery reads it before the finder chain,
    // so a Python-level deletion forces a reload and an assignment
    // substitutes the module; a None entry halts the import with ImportError.
    // Until then the internal dictionary stands in: bootstrap loads modules
    // before any Python code can touch sys.modules. CPython's machinery
    // reads interp->modules and does NOT follow a replaced sys.modules
    // attribute; the fallback mirrors that — a deleted or non-dict
    // sys.modules attribute silently reverts to the internal registry.
    // _gcd_import's cache hit: a single name with an empty fromlist
    // returns the sys.modules entry as-is, whatever its type — an
    // assignment substitutes the module wholesale. Dotted names and
    // non-empty fromlists must fall through: CPython still resolves
    // the root binding and runs _handle_fromlist after the cache hit.
    internal static bool IsBareNameCacheHit(string name, PyObject? fromList)
    {
        bool hasFromListEntry = fromList switch
        {
            PyNoneObject => false,
            PyTupleObject t => t.Count > 0,
            PyListObject l => l.Count > 0,
            _ => true
        };
        return !name.Contains('.') && !hasFromListEntry;
    }

    internal bool TryGetImportEntry(PyCallContext context, string name, [NotNullWhen(true)] out PyObject? entry)
    {
        if (TryGetModulesDict(out var modulesDict))
        {
            if (modulesDict.TryGetValue(name, out var cached))
            {
                // import.c raises ModuleNotFoundError (an ImportError
                // subclass) for the None-in-sys.modules halt
                if (cached is PyNoneObject)
                    throw context.ModuleNotFoundError(PySR.Runtime_Import_Halted, name);
                entry = cached;
                return true;
            }

            entry = null;
            return false;
        }

        entry = null;
        return false;
    }

    internal bool TryGetRegisteredModule(PyCallContext context, string name, [NotNullWhen(true)] out PyModuleObject? module)
    {
        // A replaced non-module entry is not a usable cache hit for the
        // module-typed readers; CPython assumes the mapping holds modules
        if (TryGetImportEntry(context, name, out var entry) && entry is PyModuleObject entryModule)
        {
            module = entryModule;
            return true;
        }

        if (!TryGetModulesDict(out _) && Modules.TryGetValue(name, out module))
            return module is not null;
        module = null;
        return false;
    }

    private bool TryGetModulesDict([NotNullWhen(true)] out PyDictObject? modulesDict)
    {
        if (Modules.TryGetValue("sys", out var sys) &&
            sys is not null &&
            sys.PyAttributes.TryGetValue("modules", out var modulesObj) &&
            modulesObj is PyDictObject dict)
        {
            modulesDict = dict;
            return true;
        }

        modulesDict = null;
        return false;
    }

    // The module enters both caches before its body runs (CPython
    // _load_unlocked writes sys.modules and the internal registry in one
    // step), so a reentrant import sees the partially initialized object.
    // Rolling the registration back when initialization raises keeps a
    // failed import importable again on the next attempt.
    internal void RegisterLoadedModule(string name, PyModuleObject module)
    {
        Modules[name] = module;
        if (TryGetModulesDict(out var modulesDict))
            modulesDict.SetItem(name, module);
    }

    internal void UnregisterLoadedModule(string name)
    {
        Modules.Remove(name);
        if (TryGetModulesDict(out var modulesDict))
            modulesDict.DelItem(name);
    }

    internal bool TryLoadModule(PyCallContext context, string qualifiedName, [NotNullWhen(true)] out PyModuleObject? rootModule, [NotNullWhen(true)] out PyModuleObject? module)
    {
        return InternalTryLoadModule(context, qualifiedName, out rootModule, out module, out _);
    }

    [AIGenerated]
    internal bool TryLoadModule(PyCallContext context, string qualifiedName, [NotNullWhen(true)] out PyModuleObject? rootModule, [NotNullWhen(true)] out PyModuleObject? module, out ModuleNotPackageFailure? failure)
    {
        return InternalTryLoadModule(context, qualifiedName, out rootModule, out module, out failure);
    }

    internal bool InternalTryLoadModule(PyCallContext context, string qualifiedName, [NotNullWhen(true)] out PyModuleObject? rootModule, [NotNullWhen(true)] out PyModuleObject? module, out ModuleNotPackageFailure? failure)
    {
        failure = null;
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
            {
                // the parent imported but is not a package: the error names the
                // child one level down, not the full requested name (see
                // ModuleNotPackageFailure)
                failure = new ModuleNotPackageFailure(string.Join('.', parts[..(i + 1)]), string.Join('.', parts[..i]));
                return false;
            }

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
        return InternalTryLoadModule(context, GetRootSearchPaths(context), name, out module);
    }

    // The import machinery reads sys.path itself (CPython's PySys_GetObject
    // "path" in import.c), so sys.path.append/insert from Python affects the
    // next lookup. Until the sys module exists — or when its path entry was
    // replaced by something non-iterable — the builder-configured fallback
    // list stands in.
    internal IReadOnlyList<string> GetRootSearchPaths(PyCallContext context)
    {
        if (Modules.TryGetValue("sys", out var sys) &&
            sys is not null &&
            sys.PyAttributes.TryGetValue("path", out var pathObj))
        {
            var list = PyUtils.IterableToList(context, pathObj);
            if (!list.IsError)
            {
                var paths = new List<string>(list.Value.Count);
                foreach (var item in list.Value)
                {
                    if (item is PyStrObject str)
                        paths.Add(str.Value);
                }
                return paths;
            }
        }

        return Paths;
    }

    internal bool InternalTryLoadModule(PyCallContext context, IReadOnlyList<string> paths, string qualifiedName, [NotNullWhen(true)] out PyModuleObject? module)
    {
        Debug.Assert(!qualifiedName.StartsWith('.'));

        if (TryGetRegisteredModule(context, qualifiedName, out module))
            return true;

        var frame = PyInternalFrame.CreateModuleFrame(context, isRoot: false, qualifiedName);
        using var withFrame = context.WithFrame(ref frame);

        foreach (var provider in ModuleProviders)
        {
            if (!provider.TryCreateModule(context, qualifiedName, paths, out module))
                continue;

            // Mirrors CPython's _load_unlocked: the module enters the caches
            // before its body runs, so a reentrant import (a package imported
            // from its own __init__, a circular import) finds the partially
            // initialized object instead of reentering the provider. Rolling
            // the registration back when initialization raises keeps a failed
            // import importable again on the next attempt.
            RegisterLoadedModule(qualifiedName, module);
            try
            {
                provider.ExecModule(context, module);
                module.OnImport(context, this);
            }
            catch
            {
                UnregisterLoadedModule(qualifiedName);
                throw;
            }
            return true;
        }

        return false;
    }
}

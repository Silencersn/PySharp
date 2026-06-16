using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime.Environments;

partial class PyEnvironment
{
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

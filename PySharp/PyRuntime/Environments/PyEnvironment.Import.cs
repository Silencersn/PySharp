using PySharp.PyModules.Builtins;
using PySharp.Tokenization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;
using System.Text;

namespace PySharp.PyRuntime.Environments;

partial class PyEnvironment
{
    internal PyModuleObject LoadBuiltinModule(string name)
    {
        if (Modules.TryGetValue(name, out var module))
        {
            Debug.Assert(module is not null);
            return module;
        }

        module = PyStandardLibrary.TryCreateModule(name);
        Debug.Assert(module is not null);
        module.OnImport(this);
        Modules.Add(name, module);
        return module;
    }

    internal bool TryLoadModule(string qualifiedName, [NotNullWhen(true)] out PyModuleObject? module)
    {
        if (Modules.TryGetValue(qualifiedName, out module))
            // the key may be assigned to None,
            // forcing the next import of the module to result in a ModuleNotFoundError
            return module is not null;

        return TryLoadModuleNoCache(qualifiedName, out module);
    }

    internal bool TryLoadModuleNoCache(string qualifiedName, [NotNullWhen(true)] out PyModuleObject? module)
    {
        module = PyStandardLibrary.TryCreateModule(qualifiedName);
        if (module is not null)
        {
            module.OnImport(this);
            Modules.Add(qualifiedName, module);
            return true;
        }

        if (qualifiedName.StartsWith('.'))
            throw new NotImplementedException();

        var relativeFilename = Path.Combine(qualifiedName.Split('.')) + ".py";
        foreach (var path in Paths)
        {
            var filename = Path.Combine(path, relativeFilename);
            if (!FileSystem.ExistsFile(filename))
                continue;

            var content = FileSystem.ReadAllText(filename);

            module = PyInterpreter.RunCodeWithinEnvironment(content, qualifiedName, true);
            module.OnImport(this);
            Modules.Add(qualifiedName, module);
            return true;
        }

        return false;
    }
}

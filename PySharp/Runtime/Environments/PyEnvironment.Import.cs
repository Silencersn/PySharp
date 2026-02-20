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

    internal bool TryLoadModule(PyCallContext context, string qualifiedName, [NotNullWhen(true)] out PyModuleObject? module)
    {
        if (Modules.TryGetValue(qualifiedName, out module))
            // the key may be assigned to None,
            // forcing the next import of the module to result in a ModuleNotFoundError
            return module is not null;

        return TryLoadModuleNoCache(context, qualifiedName, out module);
    }

    internal bool TryLoadModuleNoCache(PyCallContext context, string qualifiedName, [NotNullWhen(true)] out PyModuleObject? module)
    {
        var frame = PyFrame.CreateModuleFrame(context, context.CurrentFrame, qualifiedName);
        using var withFrame = context.WithFrame(frame);

        module = PyStandardLibrary.TryCreateModule(context, qualifiedName);
        if (module is not null)
        {
            module.OnImport(context, this);
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

            module = PyInterpreter.RunCodeWithContext(context, content, qualifiedName, Path.GetFullPath(filename));
            module.OnImport(context, this);
            Modules.Add(qualifiedName, module);
            return true;
        }

        return false;
    }
}

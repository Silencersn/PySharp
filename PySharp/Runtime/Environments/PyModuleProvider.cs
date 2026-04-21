using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;
using System.Text;
using System.Xml.Linq;

namespace PySharp.Runtime.Environments;

public abstract class PyModuleProvider
{
    public static PyModuleProvider Builtin => BuiltinModuleProvider.Shared;
    public static PyModuleProvider Path => PathProvider.Shared;

    public abstract bool TryGetModule(PyCallContext context, string fullName, IReadOnlyList<string>? path, [NotNullWhen(true)] out PyModuleObject? module);

    public static PyModuleProvider Create(IDictionary<string, Func<PyModuleObject>> mapping)
    {
        return new MappingModuleProvider(mapping);
    }
}

internal sealed class MappingModuleProvider : PyModuleProvider
{
    private readonly FrozenDictionary<string, Func<PyModuleObject>> _mapping;

    public MappingModuleProvider(IDictionary<string, Func<PyModuleObject>> mapping)
    {
        _mapping = mapping.ToFrozenDictionary();
    }

    public override bool TryGetModule(PyCallContext context, string fullName, IReadOnlyList<string>? path, [NotNullWhen(true)] out PyModuleObject? module)
    {
        if (_mapping.TryGetValue(fullName, out var factory))
        {
            module = factory();
            module.OnImport(context, context.PyEnvironment);
            return true;
        }

        module = null;
        return false;
    }
}

internal sealed class BuiltinModuleProvider : PyModuleProvider
{
    public static PyModuleProvider Shared { get; } = new BuiltinModuleProvider();

    public override bool TryGetModule(PyCallContext context, string fullName, IReadOnlyList<string>? path, [NotNullWhen(true)] out PyModuleObject? module)
    {
        module = PyStandardLibrary.TryCreateModule(context, fullName);
        module?.OnImport(context, context.PyEnvironment);
        return module is not null;
    }
}

internal sealed class PathProvider : PyModuleProvider
{
    public static PyModuleProvider Shared { get; } = new PathProvider();

    public override bool TryGetModule(PyCallContext context, string fullName, IReadOnlyList<string>? path, [NotNullWhen(true)] out PyModuleObject? module)
    {
        path ??= context.PyEnvironment.Paths;
        var name = fullName.Split('.')[^1];
        var fileSystem = context.PyEnvironment.Host.FileSystem;
        var pathHelper = fileSystem.PathHelper;

        foreach (var p in path)
        {
            var dir = pathHelper.Combine(p, name);
            if (fileSystem.ExistsDirectory(dir))
            {
                var package = PyModuleObject.CreatePackage(fullName, [dir]);
                var initFilename = pathHelper.Combine(dir, "__init__.py");
                if (fileSystem.ExistsFile(initFilename))
                {
                    var initCode = fileSystem.ReadAllText(initFilename);
                    PyInterpreter.RunCodeWithContext(context, initCode, package, initFilename);
                }
                module = package;
                return true;
            }

            var filename = dir + ".py";
            if (!fileSystem.ExistsFile(filename))
                continue;

            var code = fileSystem.ReadAllText(filename);
            module = PyInterpreter.RunCodeWithContext(context, code, fullName, fileSystem.GetFullPath(filename));
            module.OnImport(context, context.PyEnvironment);
            return true;
        }

        module = null;
        return false;
    }
}
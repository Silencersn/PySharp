using PySharp.Compilation;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

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

    // CPython's _load_unlocked puts the module in sys.modules before
    // exec_module runs it, and deletes that entry again if the body raises.
    // Registering first is what makes a body that imports its own package (or
    // is the target of a circular import) find the partially initialized
    // module instead of reentering this loader; rolling the registration back
    // on failure keeps a failed import importable again on the next attempt.
    private static void RunModuleBody(PyCallContext context, PyModuleObject module, string code, string filename)
    {
        context.PyEnvironment.RegisterInitializingModule(module.Name, module);
        try
        {
            PyInterpreter.RunCodeWithContext(context, code, module, filename, isMain: false);
        }
        catch
        {
            context.PyEnvironment.DiscardInitializingModule(module.Name);
            throw;
        }
    }

    public override bool TryGetModule(PyCallContext context, string fullName, IReadOnlyList<string>? path, [NotNullWhen(true)] out PyModuleObject? module)
    {
        path ??= context.PyEnvironment.Paths;
        var name = fullName.Split('.')[^1];
        var fileSystem = context.PyEnvironment.Host.FileSystem;
        var pathHelper = fileSystem.PathHelper;

        foreach (var p in path)
        {
            var dir = fileSystem.GetFullPath(pathHelper.Combine(p, name));
            if (fileSystem.ExistsDirectory(dir))
            {
                var package = PyModuleObject.CreatePackage(fullName, [dir]);
                var initFilename = pathHelper.Combine(dir, "__init__.py");
                if (fileSystem.ExistsFile(initFilename))
                {
                    // Regular package: the location is the __init__.py and
                    // must exist before the package body runs.
                    package.PyAttributes[PySpecialNames.File] = PyStrObject.FromString(initFilename);
                    var initCode = PySourceDecoder.Decode(context, fileSystem.ReadAllBytes(initFilename), initFilename);
                    RunModuleBody(context, package, initCode, initFilename);
                }
                else
                {
                    // A directory without __init__.py imports as a namespace
                    // package: no location, __file__ exposed as None.
                    package.Origin = "namespace";
                    package.PyAttributes[PySpecialNames.File] = PyNoneObject.None;
                }
                module = package;
                return true;
            }

            var filename = dir + ".py";
            if (!fileSystem.ExistsFile(filename))
                continue;

            var code = PySourceDecoder.Decode(context, fileSystem.ReadAllBytes(filename), filename);
            module = new PyModuleObject(fullName);
            // __file__ must exist before the module body runs.
            module.PyAttributes[PySpecialNames.File] = PyStrObject.FromString(filename);
            RunModuleBody(context, module, code, filename);
            module.OnImport(context, context.PyEnvironment);
            return true;
        }

        module = null;
        return false;
    }
}
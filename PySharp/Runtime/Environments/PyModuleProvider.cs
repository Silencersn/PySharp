using PySharp.Compilation;
using PySharp.Modules.Builtins;
using PySharp.Runtime.Calls;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Runtime.Environments;

// The provider protocol mirrors CPython PEP 451's two-phase loading: a
// provider locates and constructs the module (create), and the import
// machinery registers it in the module cache before handing it back for
// initialization (exec), so a reentrant import during initialization sees
// the partially initialized module instead of reentering the provider.
public abstract class PyModuleProvider
{
    public static PyModuleProvider Builtin => BuiltinModuleProvider.Shared;
    public static PyModuleProvider Path => PathProvider.Shared;

    public abstract bool TryCreateModule(PyCallContext context, string fullName, IReadOnlyList<string>? path, [NotNullWhen(true)] out PyModuleObject? module);

    // Runs the module body. The default suits providers whose create phase
    // already yields a fully initialized module (builtins, mapping factories);
    // OnImport is invoked by the machinery after this returns, so an override
    // only needs to run the body itself.
    protected internal virtual void ExecModule(PyCallContext context, PyModuleObject module) { }

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

    public override bool TryCreateModule(PyCallContext context, string fullName, IReadOnlyList<string>? path, [NotNullWhen(true)] out PyModuleObject? module)
    {
        if (_mapping.TryGetValue(fullName, out var factory))
        {
            module = factory();
            return true;
        }

        module = null;
        return false;
    }
}

internal sealed class BuiltinModuleProvider : PyModuleProvider
{
    public static PyModuleProvider Shared { get; } = new BuiltinModuleProvider();

    public override bool TryCreateModule(PyCallContext context, string fullName, IReadOnlyList<string>? path, [NotNullWhen(true)] out PyModuleObject? module)
    {
        module = PyStandardLibrary.TryCreateModule(context, fullName);
        return module is not null;
    }
}

internal sealed class PathProvider : PyModuleProvider
{
    public static PyModuleProvider Shared { get; } = new PathProvider();

    public override bool TryCreateModule(PyCallContext context, string fullName, IReadOnlyList<string>? path, [NotNullWhen(true)] out PyModuleObject? module)
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
                var package = PyPathModuleObject.CreatePackage(fullName, [dir]);
                var initFilename = pathHelper.Combine(dir, "__init__.py");
                if (fileSystem.ExistsFile(initFilename))
                {
                    // Regular package: the location is the __init__.py and
                    // must exist before the package body runs.
                    package.PyAttributes[PySpecialNames.File] = PyStrObject.FromString(initFilename);
                    package.BodyPath = initFilename;
                }
                else
                {
                    // A directory without __init__.py imports as a namespace
                    // package: no location, __file__ exposed as None, no body.
                    package.Origin = "namespace";
                    package.PyAttributes[PySpecialNames.File] = PyNoneObject.None;
                }
                module = package;
                return true;
            }

            var filename = dir + ".py";
            if (!fileSystem.ExistsFile(filename))
                continue;

            module = new PyPathModuleObject(fullName) { BodyPath = filename };
            // __file__ must exist before the module body runs.
            module.PyAttributes[PySpecialNames.File] = PyStrObject.FromString(filename);
            return true;
        }

        module = null;
        return false;
    }

    protected internal override void ExecModule(PyCallContext context, PyModuleObject module)
    {
        if (module is not PyPathModuleObject { BodyPath: { } filename })
            return;

        var fileSystem = context.PyEnvironment.Host.FileSystem;
        var code = PySourceDecoder.Decode(context, fileSystem.ReadAllBytes(filename), filename);
        PyInterpreter.RunCodeWithContext(context, code, module, filename, isMain: false);
    }
}

namespace PySharp.Runtime.IO;

public abstract class PathHelper
{
    public static PathHelper Default { get; } = new DefaultPathHelper();

    public abstract string? GetDirectoryName(string path);
    public abstract string GetFileName(string path);
    public abstract string GetFullPath(string path, string basePath);
    public abstract string? GetPathRoot(string path);
    public abstract string Combine(params ReadOnlySpan<string> paths);

    private sealed class DefaultPathHelper : PathHelper
    {
        public override string Combine(params ReadOnlySpan<string> paths)
        {
            return Path.Combine(paths);
        }
        public override string? GetDirectoryName(string path)
        {
            return Path.GetDirectoryName(path);
        }
        public override string GetFileName(string path)
        {
            return Path.GetFileName(path);
        }
        public override string GetFullPath(string path, string basePath)
        {
            return Path.GetFullPath(path, basePath);
        }
        public override string? GetPathRoot(string path)
        {
            return Path.GetPathRoot(path);
        }
    }
}
namespace PySharp.Runtime.IO;

public abstract class PathHelper
{
    public static PathHelper Default { get; } = new DefaultPathHelper();
    public static PathHelper Unix { get; } = new UnixPathHelper();

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

    private sealed class UnixPathHelper : PathHelper
    {
        public override string Combine(params ReadOnlySpan<string> paths)
        {
            if (paths.Length is 0)
                return string.Empty;

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < paths.Length; i++)
            {
                ArgumentNullException.ThrowIfNull(paths[i]);

                string p = paths[i];
                if (p.Length is 0)
                    continue;

                if (p.StartsWith('/'))
                    builder.Clear();
                else if (builder.Length > 0 && builder[^1] is not '/')
                    builder.Append('/');
                builder.Append(p);
            }
            return builder.ToString();
        }

        public override string? GetDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            for (int i = path.Length - 1; i >= 0; i--)
            {
                if (path[i] is not '/')
                    continue;

                if (i is 0)
                    return null;

                return path[..i];
            }

            return string.Empty;
        }

        public override string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            int length = path.Length;
            for (int i = length - 1; i >= 0; i--)
            {
                if (path[i] is not '/')
                    continue;

                return path[(i + 1)..];
            }
            return path;
        }

        public override string GetFullPath(string path, string basePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentException.ThrowIfNullOrEmpty(basePath);

            if (!basePath.StartsWith('/'))
                throw new ArgumentException("Base path must be fully qualified.", nameof(basePath));

            if (path.Contains('\0'))
                throw new ArgumentException("Path contains '\\0'.", nameof(path));

            if (basePath.Contains('\0'))
                throw new ArgumentException("Path contains '\\0'.", nameof(basePath));

            var combined = path.StartsWith('/') ? path : Combine([basePath, path]);
            var segments = combined.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var stack = new List<string>();

            foreach (var segment in segments)
            {
                if (segment is ".." && stack.Count > 0)
                    stack.RemoveAt(stack.Count - 1);
                else if (segment is not ".")
                    stack.Add(segment);
            }

            return "/" + string.Join("/", stack);
        }

        public override string? GetPathRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            return path.StartsWith('/') ? "/" : string.Empty;
        }
    }
}
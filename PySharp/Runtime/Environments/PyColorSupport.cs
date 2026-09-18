namespace PySharp.Runtime.Environments;

// CPython Lib/_colorize.py can_colorize: environment variables decide before
// the stream's terminal state does. null means the environment has no opinion
// and the caller falls back to its own TTY check.
internal static class PyColorSupport
{
    internal static bool? EnvironmentAllowsColor(Func<string, string?>? getenv = null)
    {
        getenv ??= System.Environment.GetEnvironmentVariable;

        // only the exact strings "0"/"1" count; anything else falls through
        if (getenv("PYTHON_COLORS") is "0")
            return false;
        if (getenv("PYTHON_COLORS") is "1")
            return true;

        // CPython treats empty NO_COLOR/FORCE_COLOR as unset
        if (!string.IsNullOrEmpty(getenv("NO_COLOR")))
            return false;
        if (!string.IsNullOrEmpty(getenv("FORCE_COLOR")))
            return true;

        if (getenv("TERM") is "dumb")
            return false;

        return null;
    }
}

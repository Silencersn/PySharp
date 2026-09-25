using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;

namespace PySharp.Tests;

[TestClass]
public sealed class AmbientContextTests
{
    private sealed class StdoutCaptureHost : PyEnvironmentHost
    {
        public MemoryStream Stdout { get; } = new();
        public override Stream AllocateStdIn() => Stream.Null;
        public override Stream AllocateStdOut() => Stdout;
        public override Stream AllocateStdErr() => Stream.Null;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }

    private static (StdoutCaptureHost Host, PyEnvironment Env, PyCallContext Context) CreateContext()
    {
        var host = new StdoutCaptureHost();
        var env = new PyEnvironment(host);
        var context = PyCallContext.CreateInterpreterRootContext(env);
        return (host, env, context);
    }

    private static string GetStdout(StdoutCaptureHost host, PyEnvironment env)
    {
        env.Out.Flush();
        return System.Text.Encoding.UTF8.GetString(host.Stdout.ToArray()).Replace("\r\n", "\n");
    }

    [TestMethod]
    public void Current_PublishesAndRestoresAcrossNestedContexts()
    {
        Assert.IsNull(PyCallContext.Current);

        var (_, _, outer) = CreateContext();
        try
        {
            Assert.AreSame(outer, PyCallContext.Current);

            var (_, _, inner) = CreateContext();
            try
            {
                Assert.AreSame(inner, PyCallContext.Current);
            }
            finally
            {
                inner.Dispose();
            }

            // the nested root restores its predecessor instead of nulling
            Assert.AreSame(outer, PyCallContext.Current);
        }
        finally
        {
            outer.Dispose();
        }

        Assert.IsNull(PyCallContext.Current);
    }

    [TestMethod]
    public void Current_NotVisibleFromOtherThreads()
    {
        // ExecutionContext flows the ambient into thread-pool work, but a
        // context is a single-thread mutable object: off-thread reads must
        // see null so fixed-signature callers keep their sentinel fallback
        var (_, _, context) = CreateContext();
        try
        {
            Assert.AreSame(context, PyCallContext.Current);

            // Task.Run would inline into the waiting pool thread (same
            // managed thread id), so use a dedicated thread for a genuine
            // off-thread read
            PyCallContext? observed = null;
            var thread = new Thread(() => observed = PyCallContext.Current);
            thread.Start();
            thread.Join();

            Assert.IsNull(observed);
        }
        finally
        {
            context.Dispose();
        }
    }

    [TestMethod]
    public void ToString_RunsUserReprOnAmbientContext()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = PyInterpreter.RunCodeWithContext(context, """
                class C:
                    def __repr__(self):
                        print("repr-side-effect")
                        return "C"

                c = C()
                """, "m", "m.py", isMain: false);

            var instance = ((IPyVariablesLocalsDict)module.PyAttributesDict)["c"];
            Assert.IsNotNull(instance);

            // object.ToString is a fixed-signature .NET face; without the
            // ambient context the __repr__ print landed on the null
            // environment and was lost
            var text = instance.ToString();

            StringAssert.Contains(GetStdout(host, env), "repr-side-effect");
            StringAssert.Contains(text, "C");
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }

    [TestMethod]
    public void ToString_FallsBackToSentinelWithoutAmbientContext()
    {
        var instance = PyStrObject.FromString("plain");

        StringAssert.Contains(instance.ToString(), "repr='plain'");
    }

    [TestMethod]
    public void ComparerDefault_CompareRunsOnAmbientContext()
    {
        var (host, env, context) = CreateContext();
        try
        {
            var module = PyInterpreter.RunCodeWithContext(context, """
                class K:
                    def __init__(self, v):
                        self.v = v

                    def __lt__(self, other):
                        print("lt-called")
                        return self.v < other.v

                a = K(2)
                b = K(1)
                """, "m", "m.py", isMain: false);

            var variables = (IPyVariablesLocalsDict)module.PyAttributesDict;
            var list = new List<PyObject> { variables["a"]!, variables["b"]! };

            // the issue headline case: a BCL sort drives the fixed-signature
            // IComparer<PyObject>.Compare, which must reach the live context
            list.Sort(PyObjectComparer.Default);

            StringAssert.Contains(GetStdout(host, env), "lt-called");
            Assert.AreSame(variables["b"], list[0]);
        }
        finally
        {
            context.Dispose();
            env.Dispose();
        }
    }
}

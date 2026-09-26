using PySharp.Compilation;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;
using System.Text;

namespace PySharp.Tests;

[TestClass]
public sealed class TestPyFiles
{
    private const string PyFilesPath = "test_pyfiles";

    private static PyModuleObject RunModule(string filename)
    {
        filename = Path.Combine(PyFilesPath, filename);
        return PyInterpreter.RunFile(filename);
    }

    private sealed class StdioHost : PyEnvironmentHost
    {
        private readonly Stream _in;
        private readonly Stream _out;
        private readonly Stream _err;

        public StdioHost(Stream input, Stream output, Stream error)
        {
            _in = input;
            _out = output;
            _err = error;
        }

        public override Stream AllocateStdIn() => _in;
        public override Stream AllocateStdOut() => _out;
        public override Stream AllocateStdErr() => _err;
        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();
    }

    private static PyModuleObject RunModuleWithHost(string filename, StdioHost host)
    {
        var path = Path.Combine(PyFilesPath, filename);
        var code = File.ReadAllText(path);
        var moduleName = Path.GetFileNameWithoutExtension(filename);
        var fullPath = Path.GetFullPath(path);

        using var environment = host
            .CreateEnvironmentBuilder()
            .AddPath(Path.GetDirectoryName(fullPath)!)
            .AddArg(fullPath)
            .Build();
        using var context = PyCallContext.CreateInterpreterRootContext(environment);
        return PyInterpreter.RunCodeWithContext(context, code, moduleName, fullPath, isMain: true);
    }

    [TestMethod]
    public void Test_Interpreter()
    {
        Assert.ThrowsExactly<PyRuntimeException>(() =>
        {
            PyInterpreter.RunCode("raise TypeError");
        });
    }

    [TestMethod]
    public void TestSysArgv()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_sys_argv.py"), ["alpha", "beta"]);
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestModuleReprOrigin()
    {
        // Module repr annotates its source the way CPython's
        // importlib._bootstrap._module_repr does — spec-less origins
        // "built-in"/"frozen" render parenthesized, a location renders as
        // "from 'path'" with __file__ recorded before the module body runs,
        // and namespace packages list their __path__ while exposing
        // __file__ as None.
        var root = Path.Combine(Path.GetTempPath(), $"modrepr_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "pkg_r"));
        Directory.CreateDirectory(Path.Combine(root, "ns_r"));
        File.WriteAllText(Path.Combine(root, "mod_r.py"), "X = 1");
        File.WriteAllText(Path.Combine(root, "pkg_r", "__init__.py"), "Y = 2");
        var script = Path.Combine(root, "repr_main.py");
        File.WriteAllText(script, TestModuleReprOriginSource);

        try
        {
            PyInterpreter.RunFile(script);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private const string TestModuleReprOriginSource = """
        # Built-in: C#-implemented stdlib modules are the analog of CPython's
        # statically linked extensions.
        import builtins
        import math
        import operator
        import queue
        import random
        import sys
        import threading
        import time
        import typing
        import warnings

        for name, module in [
            ("builtins", builtins),
            ("math", math),
            ("operator", operator),
            ("queue", queue),
            ("random", random),
            ("sys", sys),
            ("threading", threading),
            ("time", time),
            ("typing", typing),
            ("warnings", warnings),
        ]:
            assert repr(module) == f"<module '{name}' (built-in)>", repr(module)

        # Frozen: embedded-source modules.
        import dataclasses
        assert repr(dataclasses) == "<module 'dataclasses' (frozen)>", repr(dataclasses)

        # File module: __file__ is recorded before the body runs and the
        # repr names the same location.
        import mod_r
        assert mod_r.__file__.endswith("mod_r.py"), mod_r.__file__
        assert repr(mod_r) == f"<module 'mod_r' from '{mod_r.__file__}'>", repr(mod_r)

        # Regular package: the location is the __init__.py.
        import pkg_r
        assert pkg_r.__file__.endswith("__init__.py"), pkg_r.__file__
        assert repr(pkg_r) == f"<module 'pkg_r' from '{pkg_r.__file__}'>", repr(pkg_r)

        # Namespace package: parenthesized form listing __path__, no location.
        import ns_r
        assert ns_r.__file__ is None, ns_r.__file__
        expected_ns = "<module 'ns_r' (namespace) from [" + repr(ns_r.__path__[0]) + "]>"
        assert repr(ns_r) == expected_ns, repr(ns_r)

        # Script main: __file__ is available to the body.
        assert __file__.endswith("repr_main.py"), __file__
        """;

    [TestMethod]
    public void TestSyntaxWarningOnce()
    {
        // Speculative parses (statement, generator-expression
        // and group tries) must not multiply a parse-time SyntaxWarning;
        // CPython prints one warning per literal. Independent literals on
        // one line keep separate warnings, and a literal warns about its
        // first invalid escape, not the last. The module triggers five
        // legitimate warnings: one call argument, one single-element
        // tuple, two same-line literals, and the first escape of a
        // two-escape literal. Fails until the fix lands.
        var path = Path.Combine(PyFilesPath, "test_syntax_warning_once.py");
        var fullPath = Path.GetFullPath(path);
        var stderr = new MemoryStream();
        var host = new StdioHost(new MemoryStream(), new MemoryStream(), stderr);
        using var environment = host
            .CreateEnvironmentBuilder()
            .AddPath(Path.GetDirectoryName(fullPath)!)
            .AddArg(fullPath)
            .Build();
        using var context = PyCallContext.CreateInterpreterRootContext(environment);
        var code = File.ReadAllText(path);
        var module = PyInterpreter.RunCodeWithContext(
            context, code, Path.GetFileNameWithoutExtension(path), fullPath, isMain: true);
        Assert.IsNotNull(module);

        environment.Error.Flush();
        var text = System.Text.Encoding.UTF8.GetString(stderr.ToArray()).Replace("\r\n", "\n");
        var count = text.Split("is an invalid octal escape sequence").Length - 1;
        Assert.AreEqual(5, count, $"expected exactly five SyntaxWarnings, got {count}:\n{text}");
        Assert.AreEqual(0, text.Split("\"\\777\" is an invalid octal escape sequence").Length - 1,
            "the last invalid escape must not be warned instead of the first:\n" + text);
    }

    [TestMethod]
    public void TestIsLiteralWarning()
    {
        // `is`/`is not` with a constant literal operand must
        // emit a SyntaxWarning (Did you mean "=="? / "!="?), like CPython's
        // codegen_check_compare; True/False/None singletons must not warn.
        // Fails until the fix lands.
        var path = Path.Combine(PyFilesPath, "test_is_literal_warning.py");
        var fullPath = Path.GetFullPath(path);
        var stderr = new MemoryStream();
        var host = new StdioHost(new MemoryStream(), new MemoryStream(), stderr);
        using var environment = host
            .CreateEnvironmentBuilder()
            .AddPath(Path.GetDirectoryName(fullPath)!)
            .AddArg(fullPath)
            .Build();
        using var context = PyCallContext.CreateInterpreterRootContext(environment);
        var code = File.ReadAllText(path);
        var module = PyInterpreter.RunCodeWithContext(
            context, code, Path.GetFileNameWithoutExtension(path), fullPath, isMain: true);
        Assert.IsNotNull(module);

        environment.Error.Flush();
        var text = System.Text.Encoding.UTF8.GetString(stderr.ToArray()).Replace("\r\n", "\n");
        var eqCount = text.Split("Did you mean \"==\"?").Length - 1;
        var neCount = text.Split("Did you mean \"!=\"?").Length - 1;
        Assert.AreEqual(6, eqCount, $"expected 6 'is' literal warnings, got {eqCount}:\n{text}");
        Assert.AreEqual(1, neCount, $"expected 1 'is not' literal warning, got {neCount}:\n{text}");
    }

    [TestMethod]
    [Ignore("Low priority")]
    public void TestNestedCallCompileTime()
    {
        // Compiling nested calls must stay roughly linear, not
        // exponential (~24s for 22 levels on the reference machine, CPython
        // instant). The threshold is far above any linear parse and far
        // below the exponential blowup, so it only fails while the bug
        // exists. Timing assertions are environment-sensitive by nature.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var module = RunModule("test_nested_call_compile_time.py");
        sw.Stop();
        Assert.IsNotNull(module);
        Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(8),
            $"compiling 22 nested calls took {sw.Elapsed.TotalSeconds:F1}s (exponential parser blowup)");
    }

    [TestMethod]
    public void TestNestedParenthesesNoCrash()
    {
        // deeply nested parentheses must never crash the parser
        // with a StackOverflowException (the pre-fix boundary was ~156 levels
        // in Debug builds, and a stack overflow is uncatchable). The lexer now
        // rejects nesting above MaxParenLevel=100 — tighter than CPython's
        // MAXLEVEL=200, but safely below the parser's stack-overflow boundary
        // in every configuration. The crash would kill this test host, so the
        // sources are compiled in a PySharp.Console child process and only its
        // output is inspected.
        var consoleExe = PyCpythonDiffRunner.FindPySharpConsole();
        if (consoleExe is null)
            Assert.Inconclusive("PySharp.Console build output not found; build PySharp.Console first");

        (int ExitCode, string Output) RunChild(int depth)
        {
            var src = Path.Combine(Path.GetTempPath(), $"pynest_{depth}_{Guid.NewGuid():N}.py");
            File.WriteAllText(src, "x = " + new string('(', depth) + "1" + new string(')', depth) + "\n");
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(consoleExe!, $"\"{src}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var process = System.Diagnostics.Process.Start(psi)!;
                // Async reads: a crashed child may linger in Windows Error
                // Reporting with its pipes open, so the bounded exit wait
                // must come before the results are collected.
                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60_000))
                {
                    process.Kill();
                }
                var output = outTask.Result + errTask.Result;
                return (process.HasExited ? process.ExitCode : -1, output);
            }
            finally
            {
                File.Delete(src);
            }
        }

        // 100 levels: exactly at the limit, must compile and run normally
        var (shallowCode, shallowOut) = RunChild(100);
        Assert.DoesNotContain("Stack overflow", shallowOut,
            $"stack overflow crash at 100 nested parentheses:\n{shallowOut}");
        Assert.AreEqual(0, shallowCode,
            $"100 nested parentheses must compile and run (at the limit):\n{shallowOut}");

        // 101 levels: one past the limit, must be rejected with SyntaxError
        var (_, edgeOut) = RunChild(101);
        Assert.DoesNotContain("Stack overflow", edgeOut,
            $"stack overflow crash at 101 nested parentheses:\n{edgeOut}");
        Assert.Contains("too many nested parentheses", edgeOut,
            $"101 nested parentheses must raise SyntaxError (limit is 100):\n{edgeOut}");

        // 250 levels: far past the limit and past the pre-fix ~156-level
        // stack-overflow boundary, must still be rejected, never crash
        var (_, deepOut) = RunChild(250);
        Assert.DoesNotContain("Stack overflow", deepOut,
            $"stack overflow crash at 250 nested parentheses:\n{deepOut}");
        Assert.Contains("too many nested parentheses", deepOut,
            $"250 nested parentheses must raise SyntaxError (limit is 100):\n{deepOut}");
    }

    [TestMethod]
    public void TestLongBinOpChainNoCrash()
    {
        // a flat left-associated binary operator chain (3000+
        // terms) must not overflow the semantic analyzer's recursion
        // (SemanticAnalyzer.VisitExpr, uncatchable StackOverflowException;
        // CPython compiles the same source in sub-second). The crash would
        // kill this test host, so the sources are compiled in a
        // PySharp.Console child process. Both documented fix directions
        // must pass: iterative traversal (child prints the result) or a
        // recursion guard (child fails with a graceful Python error).
        // Fails until the fix lands.
        var consoleExe = PyCpythonDiffRunner.FindPySharpConsole();
        if (consoleExe is null)
            Assert.Inconclusive("PySharp.Console build output not found; build PySharp.Console first");

        (int ExitCode, string Output) RunChild(string source)
        {
            var src = Path.Combine(Path.GetTempPath(), $"binchain_{Guid.NewGuid():N}.py");
            File.WriteAllText(src, source);
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(consoleExe!, $"\"{src}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using var process = System.Diagnostics.Process.Start(psi)!;
                // Async reads: a crashed child may linger in Windows Error
                // Reporting with its pipes open, so the bounded exit wait
                // must come before the results are collected.
                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60_000))
                {
                    process.Kill();
                }
                var output = outTask.Result + errTask.Result;
                return (process.HasExited ? process.ExitCode : -1, output);
            }
            finally
            {
                File.Delete(src);
            }
        }

        static string PlusChain(int terms) =>
            "print(" + string.Join("+", System.Linq.Enumerable.Repeat("1", terms)) + ")\n";

        // red case: 5000-term chain crashes the semantic analyzer today
        var (crashCode, crashOut) = RunChild(PlusChain(5000));
        Assert.DoesNotContain("Stack overflow", crashOut,
            $"stack overflow crash on a 5000-term + chain:\n{crashOut}");
        Assert.IsTrue(
            crashOut.Contains("5000") || crashOut.Contains("Traceback"),
            $"5000-term + chain must either evaluate (iterative fix) or fail " +
            $"gracefully (recursion guard), got exit {crashCode}:\n{crashOut}");

        // guards: shapes that already work must stay working
        var (okCode, okOut) = RunChild(PlusChain(2000));
        Assert.DoesNotContain("Stack overflow", okOut, okOut);
        Assert.AreEqual(0, okCode, $"2000-term + chain must compile and run:\n{okOut}");
        Assert.Contains("2000", okOut, okOut);

        var (orCode, orOut) = RunChild(
            "print(" + string.Join(" or ", System.Linq.Enumerable.Repeat("0", 4999)) + " or 9)\n");
        Assert.DoesNotContain("Stack overflow", orOut, orOut);
        Assert.AreEqual(0, orCode, $"5000-term or chain must compile and run:\n{orOut}");
        Assert.Contains("9", orOut, orOut);
    }

    [TestMethod]
    public void TestEncodingDeclaration()
    {
        // PEP 263 source encoding declarations must be
        // honored. Today the source is always read as UTF-8 with a
        // replacement fallback, so invalid UTF-8 is silently replaced with
        // U+FFFD, latin-1/gbk declarations are ignored, and unknown codec
        // names are not validated (CPython rejects all of these). These
        // are byte-level file behaviors, so the sources are written to
        // temp files and compiled in a PySharp.Console child process.
        // Fails until the fix lands.
        var consoleExe = PyCpythonDiffRunner.FindPySharpConsole();
        if (consoleExe is null)
            Assert.Inconclusive("PySharp.Console build output not found; build PySharp.Console first");

        (int ExitCode, string StdOut, string StdErr) RunChildBytes(byte[] content)
        {
            var src = Path.Combine(Path.GetTempPath(), $"encdecl_{Guid.NewGuid():N}.py");
            File.WriteAllBytes(src, content);
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(consoleExe!, $"\"{src}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8,
                };
                using var process = System.Diagnostics.Process.Start(psi)!;
                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60_000))
                {
                    process.Kill();
                }
                return (process.HasExited ? process.ExitCode : -1,
                    outTask.Result.Replace("\r\n", "\n"), errTask.Result);
            }
            finally
            {
                File.Delete(src);
            }
        }

        static byte[] Concat(params byte[][] parts) =>
            parts.SelectMany(p => p).ToArray();
        static byte[] Ascii(string s) => System.Text.Encoding.ASCII.GetBytes(s);

        // red case 1: invalid UTF-8 without a declaration must be rejected
        // (today: silently replaced with U+FFFD and executed)
        var (c1Code, c1Out, c1Err) = RunChildBytes(Concat(
            Ascii("s = '"), new byte[] { 0xE4 }, Ascii("'\nprint(len(s))\n")));
        Assert.AreNotEqual(0, c1Code,
            $"invalid UTF-8 without declaration must be rejected:\n{c1Out}{c1Err}");
        Assert.Contains("Non-UTF-8", c1Err, c1Err);

        // red case 2: latin-1 declaration must decode 0xE4 to U+00E4
        var (c2Code, c2Out, _) = RunChildBytes(Concat(
            Ascii("# -*- coding: latin-1 -*-\ns = '"), new byte[] { 0xE4 },
            Ascii("'\nprint(repr(s))\n")));
        Assert.AreEqual(0, c2Code, c2Out);
        Assert.Contains("\u00e4", c2Out, c2Out);
        Assert.DoesNotContain("\uFFFD", c2Out, c2Out);

        // red case 3: gbk declaration must decode C4 E3 to U+4F60 (你)
        var (c3Code, c3Out, _) = RunChildBytes(Concat(
            Ascii("# -*- coding: gbk -*-\ns = '"), new byte[] { 0xC4, 0xE3 },
            Ascii("'\nprint(repr(s), len(s))\n")));
        Assert.AreEqual(0, c3Code, c3Out);
        Assert.Contains("\u4f60", c3Out, c3Out);
        Assert.DoesNotContain("\uFFFD", c3Out, c3Out);

        // red case 4: unknown codec names must be rejected
        var (c4Code, c4Out, c4Err) = RunChildBytes(Concat(
            Ascii("# -*- coding: bogus-codec-xyz -*-\nprint(\"ascii ok\")\n")));
        Assert.AreNotEqual(0, c4Code,
            $"unknown codec name must be rejected:\n{c4Out}{c4Err}");
        Assert.Contains("encoding problem", c4Err, c4Err);

        // guard: a utf-8 declaration with utf-8 content keeps working
        // (é is written as its UTF-8 bytes: Ascii() alone would mangle it
        // to '?' before the child ever runs)
        var (gCode, gOut, _) = RunChildBytes(Concat(
            Ascii("# -*- coding: utf-8 -*-\ns = \"h"), new byte[] { 0xC3, 0xA9 }, Ascii("llo\"\nprint(repr(s))\n")));
        Assert.AreEqual(0, gCode, gOut);
        Assert.Contains("\u00e9", gOut, gOut);
        Assert.DoesNotContain("\uFFFD", gOut, gOut);
    }

    [TestMethod]
    public void TestNumberTokenEndValidation()
    {
        // the lexer must validate the end of a number token
        // like CPython's verify_end_of_number: `0or` (0o prefix committed,
        // no digits) is a SyntaxError, and a complete number directly
        // followed by a keyword (1or/0.0or/0jor/0b0or) must emit a
        // SyntaxWarning instead of passing silently. The warnings are
        // asserted on the captured stderr. Fails until the fix lands.
        var path = Path.Combine(PyFilesPath, "test_number_token_end.py");
        var fullPath = Path.GetFullPath(path);
        var stderr = new MemoryStream();
        var host = new StdioHost(new MemoryStream(), new MemoryStream(), stderr);
        using var environment = host
            .CreateEnvironmentBuilder()
            .AddPath(Path.GetDirectoryName(fullPath)!)
            .AddArg(fullPath)
            .Build();
        using var context = PyCallContext.CreateInterpreterRootContext(environment);
        var code = File.ReadAllText(path);
        var module = PyInterpreter.RunCodeWithContext(
            context, code, Path.GetFileNameWithoutExtension(path), fullPath, isMain: true);
        Assert.IsNotNull(module);

        environment.Error.Flush();
        var text = System.Text.Encoding.UTF8.GetString(stderr.ToArray()).Replace("\r\n", "\n");
        Assert.Contains("SyntaxWarning", text, text);
        Assert.Contains("invalid octal literal", text, text);
        Assert.Contains("invalid decimal literal", text, text);
    }

    [TestMethod]
    public void TestSystemExitCodeExitCodeIntegration()
    {
        // pythonrun.c _Py_HandleSystemExitAndKeyboardInterrupt reads the exit
        // status from the code attribute, so a handler that rewrites or
        // deletes code controls the process exit code.
        Assert.AreEqual(0, RunExitCode("""
            try:
                raise SystemExit(2)
            except SystemExit as e:
                e.code = 0
                raise
            """), "reassigned code=0 must exit 0");

        Assert.AreEqual(0, RunExitCode("""
            try:
                raise SystemExit(2)
            except SystemExit as e:
                del e.code
                raise
            """), "deleted code must exit 0");

        Assert.AreEqual(2, RunExitCode("raise SystemExit(2)"), "int code must exit with its value");
        Assert.AreEqual(1, RunExitCode("raise SystemExit(1, 2)"), "multi-arg code must exit 1");
        Assert.AreEqual(0, RunExitCode("raise SystemExit()"), "no code must exit 0");
    }

    private static int RunExitCode(string code)
    {
        var host = new StdioHost(new MemoryStream(), new MemoryStream(), new MemoryStream());
        using var environment = host.CreateEnvironmentBuilder().Build();
        using var interpreter = PyInterpreter.Create(environment);
        try
        {
            interpreter.Execute(code, "<systemexit>");
        }
        catch (PyRuntimeException)
        {
            // the uncaught SystemExit controls the exit code through
            // PyTryCatch before rethrowing
        }
        return environment.ExitCode;
    }

    [TestMethod]
    public void TestReplDisplayHook()
    {        // interactive top-level expression statements must echo
        // through the displayhook semantics (CPython CALL_INTRINSIC_1 /
        // INTRINSIC_PRINT -> print_expr): repr(value) written to stdout -
        // so strings keep their quotes and __repr__ wins over __str__ -
        // None stays silent, and builtins._ is bound to the value. Fails
        // until the fix lands (the old path called print(), losing repr).
        var stdout = new MemoryStream();
        var host = new StdioHost(new MemoryStream(), stdout, new MemoryStream());
        using var environment = host.CreateEnvironmentBuilder().Build();
        using var context = PyCallContext.CreateInterpreterRootContext(environment);

        void ExecuteSingle(string code)
        {
            var codeObj = Compiler.InternalCompileSingle(context, code, "<stdin>", name: "<module>", appendNewLine: true);
            PyInterpreter.InternalExecute(context, codeObj);
        }

        ExecuteSingle("'hi'");
        ExecuteSingle("ascii('\\n')");
        ExecuteSingle("None");
        ExecuteSingle("1 + 1");
        ExecuteSingle("exec(\"class C:\\n    def __str__(self): return 'STR'\\n    def __repr__(self): return 'REPR'\\n\")");
        ExecuteSingle("C()");
        // resolves builtins._ inside the REPL itself and rebinds it to the
        // class name string, so the final binding is checkable as a str
        ExecuteSingle("type(_).__name__");

        var output = System.Text.Encoding.UTF8.GetString(stdout.ToArray());
        Assert.Contains("'hi'", output);
        Assert.DoesNotContain("hi\n", output);
        Assert.Contains("\"'\\\\n'\"", output);
        Assert.Contains("2", output);
        Assert.DoesNotContain("STR", output);
        Assert.Contains("REPR", output);
        Assert.Contains("'C'", output);

        // builtins._ holds the last echoed value
        var builtins = context.PyEnvironment.LoadBuiltinModule(context, "builtins");
        Assert.IsTrue(builtins.PyAttributes.TryGetValue("_", out var underscore), "builtins._ must be bound");
        var underscoreStr = Assert.IsInstanceOfType<PyStrObject>(underscore);
        Assert.AreEqual("C", underscoreStr.Value);
    }

    [TestMethod]
    public void TestFileIoError()
    {
        // File IO must follow CPython - reopening the same path
        // with a live handle is legal (CRT _SH_DENYNO) and share violations
        // surface as catchable PermissionError, seek() validates whence as
        // a ValueError, r+ close/with-exit flushes once without
        // ObjectDisposedException, "x" grants write access, and opening a
        // directory raises PermissionError. Fails until the fixes land.
        // The corpus probes "x"-mode candidates (t_file_io_x_N/xplus_N) and
        // cannot delete them itself (no os module yet): each run leaks one
        // pair into the CWD, so wipe stale artifacts first or the 100-name
        // pool exhausts after 100 runs.
        foreach (var stale in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "t_file_io_*").ToArray())
            File.Delete(stale);

        var module = RunModule("test_file_io_error.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestCallFrameCaret()
    {
        // a frame suspended in a call reports the Call instruction
        // itself (CPython's tb_lasti), so the traceback draws the call line with
        // the argument list as its anchor. The caller's instruction index used to
        // be advanced before the callee was entered, which rendered the
        // instruction after the call instead - a position that usually spans the
        // whole line and is dropped by the full-line suppression.
        var ex = Assert.ThrowsExactly<PyRuntimeException>(() =>
            PyInterpreter.RunCode("def add(a, b):\n    return a + b\n\nprint(add(1, 'x'))\n"));
        var message = LineFeedOf(ex.Message);
        StringAssert.Contains(message, "\n    print(add(1, 'x'))\n          ~~~^^^^^^^^\n");
        StringAssert.Contains(message, "\n    return a + b\n           ~~^~~\n");

        // the anchor is the call's own bracket pair, so a method call and a call
        // spanning the whole line (previously suppressed) both render
        ex = Assert.ThrowsExactly<PyRuntimeException>(() =>
            PyInterpreter.RunCode("class C:\n    def m(self, a, b):\n        return a + b\n\no = C()\no.m(1, 'x')\n"));
        message = LineFeedOf(ex.Message);
        StringAssert.Contains(message, "\n    o.m(1, 'x')\n    ~~~^^^^^^^^\n");

        // a resumed generator frame is the callee of next(), so its own position
        // is the call it is running inside
        ex = Assert.ThrowsExactly<PyRuntimeException>(() =>
            PyInterpreter.RunCode("def add(a, b):\n    return a + b\n\n\ndef gen():\n    yield add(1, 'x')\n\n\nnext(gen())\n"));
        message = LineFeedOf(ex.Message);
        StringAssert.Contains(message, "\n    next(gen())\n    ~~~~^^^^^^^\n");
        StringAssert.Contains(message, "\n    yield add(1, 'x')\n          ~~~^^^^^^^^\n");

        static string LineFeedOf(string text) => text.Replace("\r\n", "\n");
    }

    [TestMethod]
    public void TestExceptStarControlFlowSyntax()
    {
        // break/continue/return cannot cross an except* handler
        // boundary (compile-time SyntaxError); a loop inside the block and
        // a nested def are legal, and return-outside-function outranks the
        // except* error. Fails until the fix lands.
        foreach (var snippet in new[]
        {
            "def f():\n    try:\n        pass\n    except* ValueError:\n        return 1\n",
            "def f():\n    while True:\n        try:\n            pass\n        except* ValueError:\n            break\n",
            "def f():\n    while True:\n        try:\n            pass\n        except* ValueError:\n            continue\n",
            "try:\n    pass\nexcept* ValueError:\n    break\n",
            "try:\n    pass\nexcept* ValueError:\n    continue\n",
        })
        {
            var ex = Assert.ThrowsExactly<PyRuntimeException>(() => PyInterpreter.RunCode(snippet));
            StringAssert.Contains(ex.Message, "'break', 'continue' and 'return' cannot appear in an except* block");
        }

        // a loop inside the block owns its break/continue; a nested def
        // owns its return, and the function returns from outside the block
        PyInterpreter.RunCode("def f():\n    try:\n        pass\n    except* ValueError:\n        for i in range(3):\n            break\n    return 'ok'\n");
        PyInterpreter.RunCode("def f():\n    try:\n        pass\n    except* ValueError:\n        def inner():\n            return 1\n        inner()\n    return 'ok'\n");

        // at module level the return-outside-function error wins
        var ex2 = Assert.ThrowsExactly<PyRuntimeException>(() =>
            PyInterpreter.RunCode("try:\n    pass\nexcept* ValueError:\n    return 1\n"));
        StringAssert.Contains(ex2.Message, "'return' outside function");
    }

    // console host with captured streams; the encoding handover is injected
    // deterministically instead of reading the runner's Console.OutputEncoding
    private sealed class RedirectedConsoleHost : PyEnvironmentHost.ConsolePyEnvironmentHostBase
    {
        private readonly Stream _stdin;
        private readonly Stream _stdout;
        private readonly Stream _stderr;

        public RedirectedConsoleHost(Stream stdin, Stream stdout, Stream stderr)
        {
            _stdin = stdin;
            _stdout = stdout;
            _stderr = stderr;
        }

        public override IVirtualFileSystem FileSystem { get; } = MemoryFileSystem.CreateBuilder().Build();

        internal override System.Text.Encoding StdOutEncoding => System.Text.Encoding.UTF8;
        internal override System.Text.Encoding StdErrEncoding => System.Text.Encoding.UTF8;

        public override Stream AllocateStdIn() => _stdin;
        public override Stream AllocateStdOut() => _stdout;
        public override Stream AllocateStdErr() => _stderr;
    }

    [TestMethod]
    public void TestStdioNoBom()
    {
        // the console hosts hand Console.OutputEncoding - possibly
        // the BOM-emitting UTF8Encoding singleton - to the stdio writers,
        // which prepended EF BB BF to every redirected run; CPython writes
        // no preamble. The redirected console host injects Encoding.UTF8 as
        // a deterministic stand-in and inherits the real builder path.
        var stdout = new MemoryStream();
        var stderr = new MemoryStream();
        var host = new RedirectedConsoleHost(new MemoryStream(), stdout, stderr);
        var filename = "test_stdio_no_bom.py";
        var path = Path.Combine(PyFilesPath, filename);
        var code = File.ReadAllText(path);

        using (var environment = host.CreateEnvironmentBuilder()
                   .AddPath(Path.GetDirectoryName(Path.GetFullPath(path))!)
                   .AddArg(Path.GetFullPath(path))
                   .Build())
        using (var context = PyCallContext.CreateInterpreterRootContext(environment))
        {
            PyInterpreter.RunCodeWithContext(context, code, filename, Path.GetFullPath(path), isMain: true);
        }

        var outBytes = stdout.ToArray();
        Assert.IsFalse(outBytes.AsSpan().StartsWith(stackalloc byte[] { 0xEF, 0xBB, 0xBF }),
            "stdout must not start with a UTF-8 BOM: " + Convert.ToHexString(outBytes[..Math.Min(8, outBytes.Length)]));
        Assert.Contains("hello", System.Text.Encoding.UTF8.GetString(outBytes));

        var errBytes = stderr.ToArray();
        Assert.IsFalse(errBytes.AsSpan().StartsWith(stackalloc byte[] { 0xEF, 0xBB, 0xBF }),
            "stderr must not start with a UTF-8 BOM: " + Convert.ToHexString(errBytes[..Math.Min(8, errBytes.Length)]));
        Assert.Contains("err-line", System.Text.Encoding.UTF8.GetString(errBytes));
    }

    [TestMethod]
    public void TestYieldFromCloseLookupUnraisable()
    {
        // a failing `close` attribute lookup on a yield from
        // delegate is reported through the unraisable channel (CPython
        // gen_close_iter -> PyErr_FormatUnraisable) instead of escaping from
        // close(), which used to abort the caller's cleanup and leave the
        // generator suspended. A delegate close() that itself raises still
        // propagates to the caller.
        var stderr = new MemoryStream();
        var host = new StdioHost(new MemoryStream(), new MemoryStream(), stderr);
        var module = RunModuleWithHost("test_yield_from_close_lookup_unraisable.py", host);
        Assert.IsNotNull(module);

        var text = Encoding.UTF8.GetString(stderr.ToArray());
        StringAssert.Contains(text, "Exception ignored while closing generator");
        StringAssert.Contains(text, "RuntimeError: boom-getattr:close");
    }

    [TestMethod]
    public void TestThreadLifecycle()
    {
        // is_alive() on a never-started thread used to pierce
        // the process with .NET InvalidOperationException (CPython returns
        // False), a Thread subclass's run() override was never invoked
        // because the thread entry called the target directly instead of
        // dispatching through self.run, and double start() / join() before
        // start() crashed the same way instead of raising RuntimeError.
        // The corpus asserts the lifecycle in Python; the thread's uncaught
        // target exception must reach the stderr excepthook channel while
        // the main thread keeps running.
        var stderr = new MemoryStream();
        var host = new StdioHost(new MemoryStream(), new MemoryStream(), stderr);
        var module = RunModuleWithHost("test_thread_lifecycle.py", host);
        Assert.IsNotNull(module);

        var text = Encoding.UTF8.GetString(stderr.ToArray());
        StringAssert.Contains(text, "Exception in thread");
        StringAssert.Contains(text, "ValueError: kaboom");
    }
}

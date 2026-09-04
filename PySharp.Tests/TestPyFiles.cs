using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Environments;
using PySharp.Runtime.IO;
using PySharp.Runtime.IO.Memory;

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
    public void TestClassSimple()
    {
        var module = RunModule("test_class_simple.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClassInherit()
    {
        var module = RunModule("test_class_inherit.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMetaclass()
    {
        var module = RunModule("test_metaclass.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMetaclassKwargs()
    {
        var module = RunModule("test_metaclass_kwargs.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClassSpecialMethods()
    {
        var module = RunModule("test_class_special_methods.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestUserDefinedDescriptor()
    {
        var module = RunModule("test_user_defined_descriptor.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMethodDescriptor()
    {
        var module = RunModule("test_method_descriptor.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDecorator()
    {
        var module = RunModule("test_decorator.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDataclass()
    {
        var module = RunModule("test_dataclass.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFString()
    {
        var module = RunModule("test_fstring.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFStringFormat()
    {
        var module = RunModule("test_fstring_format.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestOperators()
    {
        var module = RunModule("test_operators.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAugAssignEvalOnce()
    {
        // Regression: a[b] += c / o.attr += c must evaluate the target's
        // sub-expressions exactly once (CPython 3.14 semantics).
        var module = RunModule("test_augassign_eval_once.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTypeErrorMessages()
    {
        // Regression: type() argument error messages must use the
        // "type.__new__()" prefix and match CPython 3.14 wording.
        var module = RunModule("test_type_error_messages.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestWarningsModuleBasic()
    {
        var module = RunModule("test_warnings_module_basic.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestWarningsModuleFilters()
    {
        var module = RunModule("test_warnings_module_filters.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestWarningsModuleRegistry()
    {
        var module = RunModule("test_warnings_module_registry.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestWarningsModuleActions()
    {
        var module = RunModule("test_warnings_module_actions.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestWarningsModuleCustom()
    {
        var module = RunModule("test_warnings_module_custom.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestProperty()
    {
        var module = RunModule("test_property.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestYield()
    {
        var module = RunModule("test_yield.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestList()
    {
        var module = RunModule("test_list.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDict()
    {
        var module = RunModule("test_dict.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBuiltinFuncs()
    {
        var module = RunModule("test_builtin_funcs.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDirSortingRegression()
    {
        // Regression: dir() without arguments must return names in sorted
        // order (CPython semantics), not insertion order.
        var module = RunModule("test_dir_sorting_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestContainsValueEqualityRegression()
    {
        // Regression: list/tuple `in` must use Python value equality
        // (element == item, CPython semantics) instead of reference
        // equality, so equal-but-distinct elements are found.
        var module = RunModule("test_contains_value_equality_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClosure()
    {
        var module = RunModule("test_closure.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComprehension()
    {
        var module = RunModule("test_comprehension.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAsyncComp()
    {
        var module = RunModule("test_async_comp.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAsyncCompErrors()
    {
        var module = RunModule("test_async_comp_errors.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloat()
    {
        var module = RunModule("test_float.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntParse()
    {
        var module = RunModule("test_int_parse.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestImport()
    {
        var module = RunModule("test_import.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSysArgv()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_sys_argv.py"), ["alpha", "beta"]);
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStringLiteral()
    {
        var module = RunModule("test_string_literal.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrMethods()
    {
        var module = RunModule("test_str_methods.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrMethodsExtended()
    {
        var module = RunModule("test_str_methods_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatExtended()
    {
        var module = RunModule("test_float_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFrozenSet()
    {
        var module = RunModule("test_frozenset.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestRoundVars()
    {
        var module = RunModule("test_round_vars.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestVarsDictRegression()
    {
        // Regression: vars(obj) must raise TypeError for objects without a
        // __dict__ (CPython), and obj.__dict__ must raise AttributeError,
        // instead of both returning {}.
        var module = RunModule("test_vars_dict_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestChrSurrogateRegression()
    {
        // Regression: chr() must explicitly reject surrogate code points
        // (U+D800-U+DFFF) with PySharpException instead of silently returning
        // the wrong U+FFFD replacement character.
        var module = RunModule("test_chr_surrogate_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestInt()
    {
        var module = RunModule("test_int.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestLongStr()
    {
        var module = RunModule("test_long_str.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSlice()
    {
        var module = RunModule("test_slice.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIter()
    {
        var module = RunModule("test_iter.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestNamedExpr()
    {
        var module = RunModule("test_named_expr.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStaticMethod()
    {
        var module = RunModule("test_staticmethod.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClassMethod()
    {
        var module = RunModule("test_classmethod.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestWith()
    {
        var module = RunModule("test_with.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestNameMangling()
    {
        var module = RunModule("test_name_mangling.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestException()
    {
        var module = RunModule("test_exception.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTuple()
    {
        var module = RunModule("test_tuple.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSet()
    {
        var module = RunModule("test_set.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestRange()
    {
        var module = RunModule("test_range.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestKeywordArgs()
    {
        var module = RunModule("test_keyword_args.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestZipEnumerate()
    {
        var module = RunModule("test_zip_enumerate.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMatchCase()
    {
        var module = RunModule("test_match_case.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSetFull()
    {
        var module = RunModule("test_set_full.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestByteArray()
    {
        var module = RunModule("test_bytearray.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDictExtended()
    {
        var module = RunModule("test_dict_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSortingComparison()
    {
        var module = RunModule("test_sorting_comparison.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestExceptionExtended()
    {
        var module = RunModule("test_exception_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBuiltinExtended()
    {
        var module = RunModule("test_builtin_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFunctionArgs()
    {
        var module = RunModule("test_function_args.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestCallArgsRegression()
    {
        // Covers call-args paths introduced by b440261 (Span/ArrayPool argument parsing)
        // that the rest of the suite does not exercise: >8-param buffer (ArrayPool),
        // kwonly with/without kwargs, posonly, duplicate positional/kwarg conflict, etc.
        var module = RunModule("test_call_args_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIterationExtended()
    {
        var module = RunModule("test_iteration_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMath()
    {
        var module = RunModule("test_math.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTime()
    {
        var module = RunModule("test_time.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestRandom()
    {
        var module = RunModule("test_random.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestQueue()
    {
        var module = RunModule("test_queue.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBytes()
    {
        var module = RunModule("test_bytes.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComplex()
    {
        var module = RunModule("test_complex.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTupleExtended()
    {
        var module = RunModule("test_tuple_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestRangeExtended()
    {
        var module = RunModule("test_range_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestRegressionSorting()
    {
        var module = RunModule("test_regression_sorting.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestExceptionDetails()
    {
        var module = RunModule("test_exception_details.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDictMethods()
    {
        var module = RunModule("test_dict_methods.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFunctionAttrs()
    {
        var module = RunModule("test_function_attrs.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestModuleAttrs()
    {
        var module = RunModule("test_module_attrs.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComplexExtended()
    {
        var module = RunModule("test_complex_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBytesExtended()
    {
        var module = RunModule("test_bytes_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFrozenSetExtended()
    {
        var module = RunModule("test_frozenset_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestObjectBuiltinsEdge()
    {
        var module = RunModule("test_object_builtins_edge.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestExceptionGroup()
    {
        var module = RunModule("test_exception_group.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTryExceptElse()
    {
        // Regression test: exceptions raised in the else block of
        // try-except-else should NOT be caught by the except clause
        // of the same try statement (CPython behavior).
        var module = RunModule("test_try_except_else.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestByteArrayIter()
    {
        var module = RunModule("test_bytearray_iter.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAnnotations()
    {
        var module = RunModule("test_annotations.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTypeAlias()
    {
        var module = RunModule("test_type_alias.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTString()
    {
        var module = RunModule("test_tstring.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMatchExtended()
    {
        var module = RunModule("test_match_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestUnpackExtended()
    {
        var module = RunModule("test_unpack_extended.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestNotExpr()
    {
        var module = RunModule("test_not_expr.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDefaultsEdge()
    {
        var module = RunModule("test_defaults_edge.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDefaultsNone()
    {
        var module = RunModule("test_defaults_none.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClassFreeVar()
    {
        var module = RunModule("test_class_freevar.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDelVariable()
    {
        var module = RunModule("test_del_variable.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestOpcodeEdge()
    {
        var module = RunModule("test_opcode_edge.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAsyncFor()
    {
        var module = RunModule("test_async_for.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAsyncCompRuntime()
    {
        var module = RunModule("test_async_comp_runtime.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAsyncGenerator()
    {
        var module = RunModule("test_async_generator.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBuiltinAiterAnext()
    {
        var module = RunModule("test_builtin_aiter_anext.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBuiltinMaxMinPrint()
    {
        // Regression: max()/min() key parameter, default semantics, print() file/flush
        var module = RunModule("test_builtin_maxmin_print.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestOpen()
    {
        var module = RunModule("test_open.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestOpenModeRegression()
    {
        // Regression: open() must reject modes with none of r/w/a/x
        // (''/'b'/'t'/'+'/'b+') with ValueError instead of silently opening.
        var module = RunModule("test_open_mode_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestCompileModeRegression()
    {
        // Regression: compile() with an invalid mode must raise ValueError
        // (matching CPython), not TypeError.
        var module = RunModule("test_compile_mode_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestExecEvalBuiltinsRegression()
    {
        // Regression: exec()/eval() with an explicit globals dict must inject
        // the interpreter's builtins when __builtins__ is missing (CPython),
        // instead of raising NameError for builtin names.
        var module = RunModule("test_exec_eval_builtins_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestInputEofRegression()
    {
        // Regression: input() at EOF must raise EOFError ("EOF when reading a
        // line"), not return ''. An empty stdin simulates EOF.
        var host = new StdioHost(new MemoryStream(), new MemoryStream(), new MemoryStream());
        var module = RunModuleWithHost("test_input_eof_regression.py", host);
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStdinStdoutEofRegression()
    {
        // Regression: sys.stdin.readline() at EOF returns '' (not StopIteration);
        // sys.stdout.write() returns the number of characters.
        var host = new StdioHost(new MemoryStream(), new MemoryStream(), new MemoryStream());
        var module = RunModuleWithHost("test_stdio_eof_regression.py", host);
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestReadlineEofRegression()
    {
        // Regression: open().readline() at EOF returns ''/b'' (not StopIteration),
        // while iteration raises StopIteration.
        var module = RunModule("test_readline_eof_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestListBugs()
    {
        var module = RunModule("test_list_bugs.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestListIndexStartRegression()
    {
        // Regression: list.index(x, start) must clamp an out-of-range negative
        // start to 0 (CPython), not leak a bare .NET ArgumentOutOfRangeException.
        var module = RunModule("test_list_index_start_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestListValueBugs()
    {
        var module = RunModule("test_list_value_bugs.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMemoryView()
    {
        var module = RunModule("test_memoryview.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestLongJumpExtendedArg()
    {
        var module = RunModule("test_long_jump_extended_arg.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestRelativeImport()
    {
        var module = RunModule("test_relative_import.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGenericClass()
    {
        var module = RunModule("test_generic_class.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGenericTypeVar()
    {
        var module = RunModule("test_generic_typevar.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGenericInstantiation()
    {
        var module = RunModule("test_generic_instantiation.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGenericOriginal()
    {
        var module = RunModule("test_generic_original.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGenericClosureMix()
    {
        var module = RunModule("test_generic_closure_mix.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGenericFunction()
    {
        var module = RunModule("test_generic_function.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGenericNestedDeep()
    {
        var module = RunModule("test_generic_nested_deep.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGenericNestedMixed()
    {
        var module = RunModule("test_generic_nested_mixed.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClassNestedClassVar()
    {
        var module = RunModule("test_class_nested_classvar.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComprehensionException()
    {
        // Regression test: verify inline frame cleanup when an exception
        // occurs inside a comprehension within a function's try-except.
        // See: /memories/repo/inline-frame-exception-leak.md
        var module = RunModule("test_comprehension_exception.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestForReturnCleanup()
    {
        // Regression test: verify that return inside a for-loop body
        // properly cleans up the iterator from the operand stack.
        var module = RunModule("test_for_return_cleanup.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClassClosure()
    {
        // Regression test: verify that __class__ cell variable is properly
        // propagated through ALL intermediate nested function scopes when
        // a metaclass __new__ defines closures whose inner functions use super().
        var module = RunModule("test_class_closure.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestPercentFormatRegression()
    {
        // Regression: '%c' % lone surrogates must return the surrogate (not
        // leak .NET ArgumentOutOfRangeException), huge ints must raise
        // OverflowError, and 'str % x' must not be constant-folded (so the
        // module compiles without a compile-time crash).
        var module = RunModule("test_percent_format_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntOverflowRegression()
    {
        // Regression: operations on huge ints must raise Python exceptions
        // (ValueError/OverflowError/IndexError) instead of leaking bare .NET
        // OverflowException/ArgumentOutOfRangeException.
        var module = RunModule("test_int_overflow_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatModRegression()
    {
        // Regression: float % must use Python modulo semantics (sign follows
        // the divisor): -7.0 % 3 == 2.0, not -1.0 (C# remainder semantics).
        var module = RunModule("test_float_mod_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestPowNegModRegression()
    {
        // Regression: pow(base, -exp, mod) must compute the modular inverse
        // and return an int (CPython 3.8+): pow(2, -1, 5) == 3, and raise
        // ValueError when base is not invertible for the modulus.
        var module = RunModule("test_pow_neg_mod_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestPowNegBaseModRegression()
    {
        // Regression: pow() with a negative base and a modulus must return
        // the normalized modulo result (CPython): pow(-2, 3, 5) == 2, not -3.
        var module = RunModule("test_pow_neg_base_mod_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestRoundRegression()
    {
        // Regression: round(float, ndigits) must match CPython's decimal-based
        // rounding: round(2.675, 2) == 2.67, not 2.68.
        var module = RunModule("test_round_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrCompareRegression()
    {
        // Regression: str comparison must use ordinal (code point) ordering
        // ('a' < 'B' is False), and <= / >= must not raise TypeError.
        var module = RunModule("test_str_compare_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntHexBinFormatRegression()
    {
        // Regression: hex(0)/bin(0) must keep the digit ('0x0'/'0b0'), and
        // int format()/f-string with b/o/x must not double the prefix.
        var module = RunModule("test_int_hexbin_format_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBinHexNegRegression()
    {
        // Regression: bin()/hex() on negative integers whose bit length is a
        // multiple of 8 (e.g. -128/-255/-32768) must not trip the buffer
        // Debug.Assert and must match CPython's output.
        var module = RunModule("test_bin_hex_neg_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntHexLeadingZeroRegression()
    {
        // Regression: format()/f-string int 'x'/'X' must not retain .NET's
        // sign-bit leading '0' for MSB-set values.
        var module = RunModule("test_int_hex_leading_zero_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestOctBitshiftRegression()
    {
        // Regression: oct() / format(v,'b'|'o') must match CPython for all
        // sizes including multi-byte boundaries (ToOctString/ToDigitsInBase
        // rewritten to O(n) bit extraction instead of O(n^2) division).
        var module = RunModule("test_oct_bitshift_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestPowThirdArgRegression()
    {
        // Regression: pow(x, y, mod) with a non-integer modulus must raise a
        // catchable TypeError (matching CPython) instead of terminating the
        // process via Debug.Assert in Debug builds.
        var module = RunModule("test_pow_third_arg_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestHashIntFloatRegression()
    {
        // Regression: hash invariant (equal values -> equal hashes) must hold
        // for int/float: hash(1.0) == hash(1), so {1:'a'}[1.0] works and
        // {1, 1.0} deduplicates.
        var module = RunModule("test_hash_int_float_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestHashSubclassRegression()
    {
        // Regression: hash() must return the built-in int, never an int
        // subclass instance (hash(MyInt(9)) had type MyInt, CPython returns
        // int). Also covers hash(True) is int.
        var module = RunModule("test_hash_subclass_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSliceStepZeroRegression()
    {
        // Regression: slice with step=0 must raise a catchable ValueError
        // ("slice step cannot be zero"), not leak a bare .NET exception.
        var module = RunModule("test_slice_step_zero_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestPercentFormatFloatRegression()
    {
        // Regression: old-style %e/%E must use a 2-digit exponent (not .NET's
        // fixed 3), %g must output a lowercase 'e' (not uppercase 'E'), and
        // %#g must keep trailing zeros / force a decimal point.
        var module = RunModule("test_percent_format_float_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestPercentFormatPrefixRegression()
    {
        // Regression: old-style %#o/%#x/%#X must put the sign before the
        // 0o/0x/0X prefix ('-0x10', not '0x10'), give zero a prefix too
        // ('0x0'), and zero-pad after the prefix ('-0x00010').
        var module = RunModule("test_percent_format_prefix_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatFormatRegression()
    {
        // Regression: format()/f-string float 'g'/'G'/'n' must match CPython:
        // lowercase e, zero -> '0' (not '0.0'), 'n' == 'g' under the C locale,
        // ','/'_' grouping only on the integer part of fixed-point form,
        // '_'/' ,' rejected with 'n', and 'e'/'E' exponent uses >= 2 digits.
        var module = RunModule("test_float_format_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFStringPrefixNameRegression()
    {
        // Regression: single-letter f/t function names (f, t, F, T) must not
        // be mistaken for f-string/t-string prefixes when called with a
        // string argument: f('a') / t('a') / F('a') / T('a') must compile and
        // run (also inside comprehensions), while real f-string/t-string
        // literals keep working. The lexer now validates the prefix so these
        // lex as ordinary names.
        var module = RunModule("test_fstring_prefix_name_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFStringPrefixComboRegression()
    {
        // Regression: incompatible 2-letter string-prefix combinations
        // (fB, tb, fu, ...) must not be silently accepted as f/t-strings;
        // valid f/t prefixes may only combine with r/R (all 16 case
        // variants), and unrelated bytes prefixes keep working.
        var module = RunModule("test_fstring_prefix_combo_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrStripEmptyCharsRegression()
    {
        // Regression: str.strip/lstrip/rstrip with an empty 'chars' argument
        // must strip nothing (empty chars set), not all whitespace like .NET
        // Trim(char[]) with an empty array.
        var module = RunModule("test_str_strip_empty_chars_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrReplaceEmptyOldRegression()
    {
        // Regression: str.replace('', new, count) must follow CPython's
        // interleave semantics and must not leak a raw .NET exception for the
        // default count (previously crashed the interpreter).
        var module = RunModule("test_str_replace_empty_old_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrStartsWithNegativeEndRegression()
    {
        // Regression: str.startswith/endswith must map a negative 'end' to
        // len+end (like slicing) before comparing.
        var module = RunModule("test_str_startswith_negative_end_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrEncodeErrorHandlersRegression()
    {
        // Regression: str.encode error handlers xmlcharrefreplace /
        // backslashreplace / namereplace must produce the CPython escape
        // sequences (not b'?'), and 'utf-16-le' must be a known encoding.
        var module = RunModule("test_str_encode_error_handlers_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBytesLiteralEscapeRegression()
    {
        // Regression: bytes literal escapes must follow CPython's
        // _PyBytes_DecodeEscape2 semantics: octal > 0o377 truncates to the
        // low 8 bits, and \u/\U are kept literally (not decoded like str).
        var module = RunModule("test_bytes_literal_escape_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTrailingCommaSingletonRegression()
    {
        // Regression: `expr,` (one element + trailing comma) in every
        // star_expressions position must build a 1-tuple, not degenerate
        // into the bare element. Fails until the parser fix lands.
        var module = RunModule("test_trailing_comma_singleton_regression.py");
        Assert.IsNotNull(module);
    }
}

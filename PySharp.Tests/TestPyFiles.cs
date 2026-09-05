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

    [TestMethod]
    public void TestMatchMappingNoRestRegression()
    {
        // Regression: a successfully matched mapping pattern without
        // `**rest` must not leave the keys tuple on the operand stack
        // (loop form used: the residue used to clobber the loop's
        // iterator slot -> TypeError). Fails until the fix lands.
        var module = RunModule("test_match_mapping_no_rest_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStarredPositionRegression()
    {
        // Regression: a bare starred expression (`*a`) in an illegal
        // position must raise SyntaxError instead of being silently
        // accepted with the star stripped. Fails until the fix lands.
        var module = RunModule("test_starred_position_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSyntaxWarningOnceRegression()
    {
        // Regression: the same SyntaxWarning (same file/line/message) must
        // be printed to stderr exactly once, like CPython's default warning
        // filter; a module-level lexer warning used to be printed 4 times.
        // Fails until the fix lands.
        var path = Path.Combine(PyFilesPath, "test_syntax_warning_once_regression.py");
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
        Assert.AreEqual(1, count, $"expected exactly one SyntaxWarning, got {count}:\n{text}");
    }

    [TestMethod]
    public void TestFstringDebugReprRegression()
    {
        // Regression: an f-string debug specifier `{expr=}` without an
        // explicit conversion and without a format spec must default to
        // repr() (CPython _get_interpolation_conversion), not str().
        // Fails until the fix lands.
        var module = RunModule("test_fstring_debug_repr_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntLiteralMaxDigitsRegression()
    {
        // Regression: a decimal integer literal over 4300 digits must be
        // rejected with SyntaxError (CVE-2020-10735 compile-time guard).
        // Fails until the fix lands.
        var module = RunModule("test_int_literal_max_digits_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIsLiteralWarningRegression()
    {
        // Regression: `is`/`is not` with a constant literal operand must
        // emit a SyntaxWarning (Did you mean "=="? / "!="?), like CPython's
        // codegen_check_compare; True/False/None singletons must not warn.
        // Fails until the fix lands.
        var path = Path.Combine(PyFilesPath, "test_is_literal_warning_regression.py");
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
    public void TestIntStrConversionLimitRegression()
    {
        // Regression: runtime int(str) / str(int) decimal conversions over
        // 4300 digits must raise ValueError (CVE-2020-10735 runtime guard).
        // Fails until the fix lands.
        var module = RunModule("test_int_str_conversion_limit_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestNestedCallCompileTimeRegression()
    {
        // Regression: compiling nested calls must stay roughly linear, not
        // exponential (~24s for 22 levels on the reference machine, CPython
        // instant). The threshold is far above any linear parse and far
        // below the exponential blowup, so it only fails while the bug
        // exists. Timing assertions are environment-sensitive by nature.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var module = RunModule("test_nested_call_compile_time_regression.py");
        sw.Stop();
        Assert.IsNotNull(module);
        Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(8),
            $"compiling 22 nested calls took {sw.Elapsed.TotalSeconds:F1}s (exponential parser blowup)");
    }

    [TestMethod]
    public void TestNestedParenthesesNoCrashRegression()
    {
        // Regression: deeply nested parentheses must never crash the parser
        // with a StackOverflowException (~156 levels crashes today, and a
        // stack overflow is uncatchable). CPython rejects above MAXLEVEL=200
        // with "too many nested parentheses" and accepts up to 200. The
        // crash would kill this test host, so the sources are compiled in a
        // PySharp.Console child process and only its output is inspected.
        // Fails until the fix lands.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PySharp.slnx")))
            dir = dir.Parent;
        Assert.IsNotNull(dir, "repo root (PySharp.slnx) not found above the test output");
        string? consoleExe = null;
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            var candidate = Path.Combine(dir.FullName, "PySharp.Console", "bin", cfg, "net10.0", "PySharp.Console.exe");
            if (File.Exists(candidate))
            {
                consoleExe = candidate;
                break;
            }
        }
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

        // 180 levels: below CPython's limit, must compile and run normally
        var (shallowCode, shallowOut) = RunChild(180);
        Assert.DoesNotContain("Stack overflow", shallowOut,
            $"stack overflow crash at 180 nested parentheses:\n{shallowOut}");
        Assert.AreEqual(0, shallowCode,
            $"180 nested parentheses must compile and run (CPython allows up to 200):\n{shallowOut}");

        // 250 levels: above CPython's MAXLEVEL=200, must be rejected
        // gracefully with SyntaxError
        var (deepCode, deepOut) = RunChild(250);
        Assert.DoesNotContain("Stack overflow", deepOut,
            $"stack overflow crash at 250 nested parentheses:\n{deepOut}");
        Assert.Contains("too many nested parentheses", deepOut,
            $"250 nested parentheses must raise SyntaxError (CPython MAXLEVEL=200):\n{deepOut}");
    }

    [TestMethod]
    public void TestMaxIndentRegression()
    {
        // Regression: more than 100 levels of indentation must be rejected
        // with IndentationError (CPython MAXINDENT=100). Fails until the
        // fix lands.
        var module = RunModule("test_max_indent_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAsyncContextValidationRegression()
    {
        // Regression: `async for`/`async with` in a sync function and
        // `return <value>` in an async generator must be rejected with
        // SyntaxError (CPython symtable/codegen context checks). Fails
        // until the fix lands.
        var module = RunModule("test_async_context_validation_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClassScopeComprehensionRegression()
    {
        // Regression: comprehension bodies in a class scope must skip the
        // class scope when resolving names (only the outermost iterable is
        // evaluated in the class scope, CPython symtable rule). Fails until
        // the fix lands.
        var module = RunModule("test_class_scope_comprehension_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBytesLiteralAsciiRegression()
    {
        // Regression: a bytes literal with any non-ASCII character must be
        // rejected with "bytes can only contain ASCII literal characters"
        // (no Latin-1 silent acceptance, no unicodeescape error path).
        // Fails until the fix lands.
        var module = RunModule("test_bytes_literal_ascii_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFormFeedIndentRegression()
    {
        // Regression: a form feed inside indentation must reset the column
        // counter to zero (CPython lexer.c:529), not count as indent
        // whitespace. Fails until the fix lands.
        var module = RunModule("test_form_feed_indent_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSourceNullBytesRegression()
    {
        // Regression: a NUL byte anywhere in the source (string/bytes
        // literals, comments included) must be rejected with SyntaxError
        // like CPython's contains_null_bytes check. Fails until the fix
        // lands.
        var module = RunModule("test_source_null_bytes_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTabErrorRegression()
    {
        // Regression: tab/space mixed indentation errors must raise the
        // TabError subclass (CPython picks the type from the message), not
        // the plain parent IndentationError. Fails until the fix lands.
        var module = RunModule("test_tab_error_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestLongBinOpChainNoCrashRegression()
    {
        // Regression: a flat left-associated binary operator chain (3000+
        // terms) must not overflow the semantic analyzer's recursion
        // (SemanticAnalyzer.VisitExpr, uncatchable StackOverflowException;
        // CPython compiles the same source in sub-second). The crash would
        // kill this test host, so the sources are compiled in a
        // PySharp.Console child process. Both documented fix directions
        // must pass: iterative traversal (child prints the result) or a
        // recursion guard (child fails with a graceful Python error).
        // Fails until the fix lands.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PySharp.slnx")))
            dir = dir.Parent;
        Assert.IsNotNull(dir, "repo root (PySharp.slnx) not found above the test output");
        string? consoleExe = null;
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            var candidate = Path.Combine(dir.FullName, "PySharp.Console", "bin", cfg, "net10.0", "PySharp.Console.exe");
            if (File.Exists(candidate))
            {
                consoleExe = candidate;
                break;
            }
        }
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
    public void TestEncodingDeclarationRegression()
    {
        // Regression: PEP 263 source encoding declarations must be
        // honored. Today the source is always read as UTF-8 with a
        // replacement fallback, so invalid UTF-8 is silently replaced with
        // U+FFFD, latin-1/gbk declarations are ignored, and unknown codec
        // names are not validated (CPython rejects all of these). These
        // are byte-level file behaviors, so the sources are written to
        // temp files and compiled in a PySharp.Console child process.
        // Fails until the fix lands.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PySharp.slnx")))
            dir = dir.Parent;
        Assert.IsNotNull(dir, "repo root (PySharp.slnx) not found above the test output");
        string? consoleExe = null;
        foreach (var cfg in new[] { "Debug", "Release" })
        {
            var candidate = Path.Combine(dir.FullName, "PySharp.Console", "bin", cfg, "net10.0", "PySharp.Console.exe");
            if (File.Exists(candidate))
            {
                consoleExe = candidate;
                break;
            }
        }
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
        var (gCode, gOut, _) = RunChildBytes(Concat(
            Ascii("# -*- coding: utf-8 -*-\ns = \"h\u00e9llo\"\nprint(repr(s))\n")));
        Assert.AreEqual(0, gCode, gOut);
        Assert.Contains("\u00e9", gOut, gOut);
        Assert.DoesNotContain("\uFFFD", gOut, gOut);
    }

    [TestMethod]
    public void TestBareStarKwargsRegression()
    {
        // Regression: a bare `*` in a parameter list must be followed by at
        // least one named keyword-only parameter (CPython: "named arguments
        // must follow bare *"). Fails until the fix lands.
        var module = RunModule("test_bare_star_kwargs_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestModuleAttrMessageRegression()
    {
        // Regression: a module attribute miss must produce a clean message
        // ("module 'sys' has no attribute 'x'"), not leak the attribute
        // name's PyStrObject debug dump into the user-visible text. Fails
        // until the fix lands.
        var module = RunModule("test_module_attr_message_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestNumberTokenEndValidationRegression()
    {
        // Regression: the lexer must validate the end of a number token
        // like CPython's verify_end_of_number: `0or` (0o prefix committed,
        // no digits) is a SyntaxError, and a complete number directly
        // followed by a keyword (1or/0.0or/0jor/0b0or) must emit a
        // SyntaxWarning instead of passing silently. The warnings are
        // asserted on the captured stderr. Fails until the fix lands.
        var path = Path.Combine(PyFilesPath, "test_number_token_end_regression.py");
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
}

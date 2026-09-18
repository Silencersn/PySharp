using PySharp.Compilation;
using PySharp.Modules.Builtins;
using PySharp.Runtime;
using PySharp.Runtime.Calls;
using PySharp.Runtime.Calls.Extensions;
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
    public void TestIterCallableSentinelRegression()
    {
        // Regression: iter(callable, sentinel) must return a callable
        // iterator that calls the callable with no args until the result
        // equals the sentinel (sentinel as the left == operand), swallows
        // StopIteration as a clean exhaustion, propagates other errors,
        // and stops calling the callable once exhausted. Fails until the
        // fix lands.
        var module = RunModule("test_iter_callable_sentinel_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrTailmatchTupleRegression()
    {
        // Regression: str.startswith/endswith must accept a tuple of
        // prefixes/suffixes (any match wins) and reject non-str items and
        // non-str non-tuple first args with CPython's exact messages,
        // while the str-path window semantics stay untouched. Fails until
        // the fix lands.
        var module = RunModule("test_str_tailmatch_tuple_regression.py");
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
    public void TestPropertyRegression()
    {
        // Regression: assigning to or deleting a property without the
        // matching accessor must raise AttributeError naming the property
        // and the owner type (never a None-callable TypeError),
        // property.__doc__ must inherit the getter's docstring when no
        // explicit doc is given, getter/setter/deleter must return copies,
        // and __name__/fget/fset/fdel must be exposed. Fails until the
        // fixes land.
        var module = RunModule("test_property_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMathFloorCeilTruncRegression()
    {
        // Regression: math.floor/ceil/trunc must return int like CPython
        // 3.14 - exact floats convert straight to int with the inf/nan
        // conversion errors, non-float objects try the protocol method
        // first (floor/ceil fall back to a float/index conversion, trunc
        // reports the missing method), and int defines all three as its
        // identity with bool converting to a pooled exact int.
        // Fails until the fix lands.
        var module = RunModule("test_math_floor_ceil_trunc_regression.py");
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
    public void TestMatchValuePatternRegression()
    {
        // Regression: dotted value patterns (case Color.RED:) used to put the
        // parser into an infinite ParseAttr <-> ParseNameOrAttr recursion,
        // crashing the process with an uncatchable .NET stack overflow at
        // compile time. Fails until the fix lands.
        var module = RunModule("test_match_value_pattern_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMatchNegativeLiteralPatternRegression()
    {
        // Regression: negative numeric literal patterns (case -1: / case -1.5:)
        // are valid per PEP 634 signed_number but used to be rejected with
        // SyntaxError; '+1' and minus before non-numbers keep being rejected.
        // Fails until the fix lands.
        var module = RunModule("test_match_negative_literal_pattern_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMatchDuplicateCaptureRegression()
    {
        // Regression: a capture name repeated inside one pattern (case [a, a]:)
        // must be rejected with "multiple assignments to name 'a' in pattern"
        // like CPython codegen_pattern_helper_store_name, instead of silently
        // matching everything with the later binding shadowing the earlier;
        // the same name across or-pattern alternatives and across cases stays
        // legal. Fails until the fix lands.
        var module = RunModule("test_match_duplicate_capture_regression.py");
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
    public void TestStrUtf16Utf32BomRegression()
    {
        // Regression: bare 'utf-16' / 'utf-32' must emit a BOM on encode and
        // consume a leading BOM (selecting the byte order) on decode; the
        // explicit -le/-be variants stay BOM-free on both sides.
        var module = RunModule("test_str_utf16_utf32_bom_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestPowZeroNegativeExponentRegression()
    {
        // Regression: 0/0.0 (including -0.0) to a negative power must raise
        // ZeroDivisionError on every ** path, not return inf.
        var module = RunModule("test_pow_zero_negative_exponent_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestPep479GeneratorStopIterationRegression()
    {
        // Regression: PEP 479 — a StopIteration escaping a generator frame
        // must become RuntimeError (with __cause__), not normal exhaustion;
        // coroutines and async generators use their own messages.
        var module = RunModule("test_pep479_generator_stopiteration_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntFloatConversionRoundingRegression()
    {
        // Regression: BigInteger -> double conversion must round to nearest,
        // ties to even (guard + sticky bits); the plain (double) cast
        // truncated and landed 1 ulp low (e.g. float(10**30)).
        var module = RunModule("test_int_float_conversion_rounding_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatIntNanInfRegression()
    {
        // Regression: int(nan/inf) must raise catchable ValueError /
        // OverflowError (not a .NET crash), and round(nan/inf) the same
        // instead of a bare TypeError.
        var module = RunModule("test_float_int_nan_inf_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrFormatNumberingModeRegression()
    {
        // Regression: mixing automatic '{}' and manual '{N}' numbering in
        // str.format must raise CPython's ValueError; the mode is shared
        // with nested format specs and kwargs are exempt.
        var module = RunModule("test_str_format_numbering_mode_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestRangeContainsRegression()
    {
        // Regression: `x in range` must take CPython's O(1) arithmetic test
        // for int/bool operands (huge ranges used to hang); non-int operands
        // keep enumerating.
        var module = RunModule("test_range_contains_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestCallableTypeLevelLookupRegression()
    {
        // Regression: callable() probes __call__ on the type (tp_call
        // semantics) — instance __getattr__/__getattribute__ hooks must not
        // fire and cannot fake a True result.
        var module = RunModule("test_callable_type_level_lookup_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestReflectedArithmeticSameTypeRegression()
    {
        // Regression: identical operand types share one binary slot — only
        // the forward variant runs and __radd__ etc. never fire; comparisons
        // keep the reflected direction; arithmetic TypeErrors use CPython's
        // "unsupported operand type(s)" wording.
        var module = RunModule("test_reflected_arithmetic_same_type_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSetSubclassReprRegression()
    {
        // Regression: set/frozenset subclass instances render TypeName({…})
        // (or TypeName() when empty) like CPython; dict/list/tuple
        // subclasses keep the bare builtin form.
        var module = RunModule("test_set_subclass_repr_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFileReadlinesRegression()
    {
        // Regression: file objects must expose readlines() with CPython's
        // hint semantics (stop after the total size EXCEEDS a positive
        // hint), including binary mode and closed-file ValueError.
        var module = RunModule("test_file_readlines_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComplexAbsRegression()
    {
        // Regression: abs() of a complex value must return hypot(real, imag)
        // as a float (CPython complex_abs), with OverflowError when the
        // hypot overflows for finite components.
        var module = RunModule("test_complex_abs_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComplexKeywordConstructorRegression()
    {
        // Regression: complex() must bind 'real'/'imag' keyword arguments
        // (CPython complex_new_impl) instead of silently ignoring them, with
        // CPython's conflict/count/type-error messages and arithmetic
        // combining of complex component values.
        var module = RunModule("test_complex_keyword_constructor_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComplexReprPureImaginaryRegression()
    {
        // Regression: complex str/repr must follow CPython complex_repr — a
        // +0.0 real part drops the parens, a -0.0 real part keeps them, and
        // the imaginary part keeps its sign including -0.0.
        var module = RunModule("test_complex_repr_pure_imaginary_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComplexReflectedArithmeticRegression()
    {
        // Regression: int/float/bool on the left of + - * / ** must dispatch
        // the complex reflected slots (CPython's order-agnostic slots), and
        // forward complex ** must follow complex_pow (c_powi squaring,
        // c_pow log/exp form, EDOM/ERANGE error mapping).
        var module = RunModule("test_complex_reflected_arithmetic_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComprehensionClosureCaptureRegression()
    {
        // Regression: closures capturing comprehension loop variables need a
        // cell owned by the comprehension scope — the lambda/genexpr prologues
        // create CellVars, and class-body inline comprehensions participate in
        // capture analysis instead of leaking the name to the module globals.
        var module = RunModule("test_comprehension_closure_capture_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestLambdaParamCellRegression()
    {
        // Regression: a lambda parameter captured by a nested lambda or
        // generator expression must be cell-ized like a def parameter — the
        // lambda scope emits a CellVars prologue so closure packing gets real
        // cells instead of aborting the process on plain parameter values.
        var module = RunModule("test_lambda_param_cell_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestWithBreakContinueRegression()
    {
        // Regression: break/continue/return crossing with blocks must inline
        // the __exit__ cleanup and drop the handler record before jumping —
        // bare jumps skipped __exit__, leaked the [exit, manager] pair
        // (stack assert / iterator corruption / overflow) and left a dead
        // handler record that misdispatched later exceptions.
        var module = RunModule("test_with_break_continue_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFinallyControlFlowRegression()
    {
        // Regression: break/continue/return crossing try/finally must copy
        // the finally body inline into the jump (record dropped first);
        // leaving an except handler body clears the in-flight exception and
        // deletes the `except ... as name` binding.
        var module = RunModule("test_finally_control_flow_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAsyncControlFlowRegression()
    {
        // Regression: break/continue/return crossing async with inlines the
        // awaited __aexit__(None, None, None) sequence, and jumping out of
        // an async-for body drops its await-watching record; both used to
        // leak stack items and handler records.
        var module = RunModule("test_async_control_flow_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGenexpWalrusPromotionRegression()
    {
        // Regression: PEP 572 walrus targets inside a genexp bind in the
        // nearest enclosing non-comprehension scope (function local through a
        // shared cell, or the global scope), matching CPython's
        // symtable_extend_namedexpr_scope; empty-cell reads distinguish
        // cellvar (UnboundLocalError) from freevar (NameError) wording.
        var module = RunModule("test_genexp_walrus_promotion_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComprehensionCallInlineFrameRegression()
    {
        // Regression: a call made inside an inlined comprehension frame must
        // not rebind the enclosing frame's locals to the disposed inline span
        // (_ExitInlineFrame now rebinds), and unbound fast-local/cell errors
        // report the source variable name instead of a slot index.
        var module = RunModule("test_comprehension_call_inline_frame_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestExceptAsImplicitCleanupRegression()
    {
        // Regression: the implicit except-as cleanup is `name = None;
        // del name` and runs on every handler exit (normal, escape, return,
        // except* both paths) — a plain del raised an uncatchable NameError
        // after an explicit del in the body, and escapes leaked the binding.
        var module = RunModule("test_except_as_implicit_cleanup_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestWithMultiEnterCleanupRegression()
    {
        // Regression: a with-item's handler region restores the stack to
        // before the __enter__ result (CPython SETUP_WITH semantics) — a
        // later manager's __enter__ failing, body operand temporaries, or an
        // as-target unpack failure used to leave strays that made the
        // handler call the manager object (TypeError) and skip __exit__.
        var module = RunModule("test_with_multi_enter_cleanup_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestEvalExecCallerLocalsRegression()
    {
        // Regression: eval/exec with default globals/locals use the calling
        // frame's namespaces (CPython fromframe) — function locals were
        // invisible to eval (wrong global value or NameError) and exec/walrus
        // writes leaked into module globals; explicit `global` declarations
        // in exec'd code still target the real globals.
        var module = RunModule("test_eval_exec_caller_locals_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestObjectNewExcessArgsRegression()
    {
        // Regression: excess constructor arguments on classes using the
        // object-default __new__/__init__ raise TypeError like CPython
        // object_new/object_init ("<Cls>() takes no arguments"), covering
        // subclasses (the old check only matched exact object); custom
        // __init__/__new__, object.__init__ direct calls, cooperative
        // super().__init__ chains, the exception family, type.__init__'s
        // 1/3-argument forms, and re-assigning the inherited defaults all
        // follow the CPython rules.
        var module = RunModule("test_object_new_excess_args_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComplexHashRegression()
    {
        // Regression: complex hashes agree with int/float hashes for equal
        // values (CPython complex_hash: the parts' double hashes combined
        // with _PyHASH_IMAG, a zero imaginary part keeps the real hash), so
        // cross-type dict/set lookups no longer silently miss.
        var module = RunModule("test_complex_hash_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFStringColonEqualSpecRegression()
    {
        // Regression: a bare ':' inside an f-string/t-string replacement field
        // ends the expression and starts the format spec, even when lexed as
        // the fused ':=' token (PEP 572 requires parentheses for a walrus),
        // so f"{-5:=8}" == format(-5, "=8"); debug '=' forms and parenthesized
        // walrus expressions are unaffected.
        var module = RunModule("test_fstring_colonequal_spec_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestRangeSliceEqHashRegression()
    {
        // Regression: range and slice compare by their parameters (CPython
        // range_equals: empty ranges equal, single-element ranges ignore the
        // step; slice_richcompare: (start, stop, step) part-wise) and hash
        // consistently with that equality (range_hash's
        // (length, start, step) tuple, slice_hash's parts tuple), making
        // equal instances usable as dict keys and set elements.
        var module = RunModule("test_range_slice_eq_hash_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestWarningsCustomCategoryRegression()
    {
        // Regression: warnings.warn with a user-defined Warning subclass
        // records the warning like a built-in category (the instance is
        // created by calling the category, not by casting the type object
        // to the built-in PyExceptionType — the old hard cast crashed the
        // process with a .NET InvalidCastException); non-Warning categories
        // raise TypeError and the error filter raises the custom category.
        var module = RunModule("test_warnings_custom_category_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFromImportErrorRegression()
    {
        // Regression: a from-import of a name missing from a package raises
        // ImportError ("cannot import name '<name>' from '<package>'") like
        // CPython import_from, instead of letting the underlying
        // AttributeError escape so `except ImportError` could not catch it;
        // plain attribute access on the module stays AttributeError.
        var module = RunModule("test_from_import_error_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMatchMissingMatchArgsRegression()
    {
        // Regression: a class pattern with positional sub-patterns against a
        // class without __match_args__ raises the CPython arity TypeError
        // ("<Name>() accepts 0 positional sub-patterns (N given)") instead of
        // leaking the attribute lookup error as AttributeError; the plural
        // marker follows CPython (omitted when the allowed count is 1).
        var module = RunModule("test_match_missing_match_args_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBoolNumericSlotsRegression()
    {
        // Regression: bool's numeric slots follow the CPython boolobject.c
        // specialization — bitwise and/or/xor keep bool when both operands
        // are bool (int otherwise), while unary + (like -, ~, abs) upgrades
        // to int; the old behavior was exactly inverted.
        var module = RunModule("test_bool_numeric_slots_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntBytesRegression()
    {
        // Regression: int() accepts bytes-like arguments (bytes, bytearray,
        // memoryview), parsing their ASCII characters with the given base
        // like CPython PyNumber_Long; invalid literals raise ValueError
        // quoting the bytes repr instead of the contradictory TypeError
        // that claimed bytes-like objects were accepted.
        var module = RunModule("test_int_bytes_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestConcatReflectedRaddRegression()
    {
        // Regression: str/bytes/tuple `+` give the right operand's __radd__
        // the first chance (CPython has no nb_add for them — the reflected
        // slot runs before the sq_concat fallback); the concat TypeError is
        // the last resort with unchanged messages.
        var module = RunModule("test_concat_reflected_radd_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestExceptionCustomNewArgsRegression()
    {
        // Regression: an exception subclass overriding __new__ but not
        // __init__ still records the original instantiation arguments in
        // e.args — the inherited BaseException.__init__ re-binds the args
        // tuple (CPython BaseException_init), and rejects keywords with
        // "<Cls>() takes no keyword arguments".
        var module = RunModule("test_exception_custom_new_args_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatPowOverflowRegression()
    {
        // Regression: float `**` (and pow()) overflowing the double range
        // raises OverflowError with the errno args tuple like CPython
        // float_pow's ERANGE handling, instead of silently returning
        // +/-inf; underflow and non-finite operands stay non-errors.
        var module = RunModule("test_float_pow_overflow_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGeneratorReentrancyRegression()
    {
        // Regression: consuming a generator while it is already executing
        // raises the catchable ValueError "generator already executing"
        // (CPython gi_running guard) on the next/send, throw and close
        // resume paths, instead of recursing into the evaluator until a
        // .NET IndexOutOfRangeException terminates the process.
        var module = RunModule("test_generator_reentrancy_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBareRaiseDynamicScopeRegression()
    {
        // Regression: bare raise reads the call chain's handled exception
        // (CPython thread exc_info) — helpers called from except bodies,
        // __exit__ during a with-body exception, and finally pass-through all
        // reraise the active exception, and the state is restored once the
        // handler completes so later bare raises see no active exception.
        var module = RunModule("test_bare_raise_dynamic_scope_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFrozenSetConstructorBaseTypeRegression()
    {
        // Regression: frozenset() must return an exact frozenset unchanged
        // but copy subclass instances into the base type (CPython
        // make_new_set); constructing a subclass must never retype the
        // original object in place.
        var module = RunModule("test_frozenset_constructor_base_type_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestNumericUnderscoreLiteralRegression()
    {
        // Regression: PEP 515 underscore separators must work in every
        // numeric literal form (float mantissa/fraction/exponent, complex,
        // prefixed ints) instead of handing an empty string to double.Parse,
        // and invalid placements keep the tokenize-time SyntaxError.
        var module = RunModule("test_numeric_underscore_literal_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestNotImplementedBoolRegression()
    {
        // Regression: NotImplemented in a boolean context must raise
        // CPython's TypeError; as a plain value it stays legal.
        var module = RunModule("test_notimplemented_bool_regression.py");
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
        // Regression: speculative parses (statement, generator-expression
        // and group tries) must not multiply a parse-time SyntaxWarning;
        // CPython prints one warning per literal. Independent literals on
        // one line keep separate warnings, and a literal warns about its
        // first invalid escape, not the last. The module triggers five
        // legitimate warnings: one call argument, one single-element
        // tuple, two same-line literals, and the first escape of a
        // two-escape literal. Fails until the fix lands.
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
        Assert.AreEqual(5, count, $"expected exactly five SyntaxWarnings, got {count}:\n{text}");
        Assert.AreEqual(0, text.Split("\"\\777\" is an invalid octal escape sequence").Length - 1,
            "the last invalid escape must not be warned instead of the first:\n" + text);
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
    [Ignore("Low priority")]
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
        // with a StackOverflowException (the pre-fix boundary was ~156 levels
        // in Debug builds, and a stack overflow is uncatchable). The lexer now
        // rejects nesting above MaxParenLevel=100 — tighter than CPython's
        // MAXLEVEL=200, but safely below the parser's stack-overflow boundary
        // in every configuration. The crash would kill this test host, so the
        // sources are compiled in a PySharp.Console child process and only its
        // output is inspected.
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
    public void TestFormatSpecStrRegression()
    {
        // Regression: format specs on str values and the str.format field
        // grammar (conversions, nested specs, [index]/.attr access) were
        // unimplemented - see the module docstring for the full symptom
        // list. Fails until the fix lands.
        var module = RunModule("test_format_spec_str_regression.py");
        Assert.IsNotNull(module);
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
    public void TestNextNonIteratorRegression()
    {
        // Regression: next(x) on a non-iterator must raise
        // TypeError: '<type>' object is not an iterator (CPython
        // builtin_next), not "iter() returned non-iterator of type '...'";
        // __iter__ results lacking __next__ must still be rejected with
        // the iter() message. Fails until the fix lands.
        var module = RunModule("test_next_non_iterator_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntDivmodRegression()
    {
        // Regression: integer divmod() must use floor semantics (CPython
        // l_divmod) and raise ZeroDivisionError on a zero divisor, not
        // .NET's truncated DivRem and a native DivideByZeroException;
        // the // shortcut path must floor to -1 for |a| < |b| with
        // opposite signs. Fails until the fix lands.
        var module = RunModule("test_int_divmod_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatNoArgsRegression()
    {
        // Regression: float() with no arguments must return 0.0 (CPython
        // float_new), not TypeError; invalid strings must raise ValueError
        // with the CPython message, and float subclasses must construct
        // subclass instances. Fails until the fix lands.
        var module = RunModule("test_float_noargs_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestListPopEmptyRegression()
    {
        // Regression: pop() on an empty list must raise
        // IndexError: pop from empty list (CPython list_pop_impl), for any
        // index; a non-empty list keeps "pop index out of range". Fails
        // until the fix lands.
        var module = RunModule("test_list_pop_empty_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestListNegSetItemRegression()
    {
        // Regression: negative-index item assignment must map the index
        // (CPython list_ass_subscript) instead of crashing on a raw .NET
        // negative index; out-of-range assignment reports the assignment
        // message. Fails until the fix lands.
        var module = RunModule("test_list_neg_setitem_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestLambdaStarRegression()
    {
        // Regression: a lambda's *args parameter must not swallow the
        // body colon as a star annotation (ParseParamStarAnnotation);
        // 'lambda *a: a' failed with a bare SyntaxError. Fails until
        // the fix lands.
        var module = RunModule("test_lambda_star_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDictBigHashRegression()
    {
        // Regression: a dict key's hash feeds only the probe, so the
        // full-precision hash must not go through the throwing Int32Value
        // conversion; out-of-int32-range hashes used to raise a bare
        // OverflowError on every key operation. Fails until the fix lands.
        var module = RunModule("test_dict_big_hash_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestInitReturnCheckRegression()
    {
        // Regression: __init__ must return None when called through
        // instantiation (CPython slot_tp_init), also via a metaclass; a
        // direct __init__() call stays exempt. Fails until the fix lands.
        var module = RunModule("test_init_return_check_regression.py");
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
        // (é is written as its UTF-8 bytes: Ascii() alone would mangle it
        // to '?' before the child ever runs)
        var (gCode, gOut, _) = RunChildBytes(Concat(
            Ascii("# -*- coding: utf-8 -*-\ns = \"h"), new byte[] { 0xC3, 0xA9 }, Ascii("llo\"\nprint(repr(s))\n")));
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

    [TestMethod]
    public void TestBracketNeverClosedRegression()
    {
        // Regression: the lexer must report an opener still on its bracket
        // stack at EOF (CPython "'X' was never closed", innermost opener),
        // plus mismatched closes, instead of leaking to the parser where
        // the input died with misleading messages. Fails until the fix
        // lands.
        var module = RunModule("test_bracket_never_closed_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestEmptyNeedleTailmatchRegression()
    {
        // Regression: an empty prefix/suffix must match at every valid
        // startswith/endswith window position (CPython tailmatch), including
        // "".endswith(""); a start past the length stays False. Fails until
        // the fix lands.
        var module = RunModule("test_empty_needle_tailmatch_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntNewBoolSingletonRegression()
    {
        // Regression: int(True)/int(False) must convert through the small-int
        // pool instead of retagging the process-wide bool singletons (type
        // and repr of True/False stayed corrupted afterwards). Fails until
        // the fix lands.
        var module = RunModule("test_int_new_bool_singleton_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntRoundRegression()
    {
        // Regression: round() on int must work in every ndigits form like
        // CPython long_round (round(2) == 2, round(True) == 1 as a pooled
        // exact int) and the slot-missing fallback must carry a real
        // message, never an empty TypeError. Fails until the fix lands.
        var module = RunModule("test_int_round_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAbsRegression()
    {
        // Regression: float abs must clear the sign bit (abs(-0.0) is
        // +0.0 and math.copysign sees a positive result), and int.__abs__
        // must convert bool instances to pooled exact ints. Fails until
        // the fix lands.
        var module = RunModule("test_abs_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBoundMethodEqRegression()
    {
        // Regression: bound method objects must compare equal when both
        // __func__ and __self__ are identical (identity comparison, so a
        // custom __eq__ on the instance does not leak in), carry a
        // consistent hash for `in`/set/remove usage, and reject ordering
        // comparisons. Fails until the fix lands.
        var module = RunModule("test_bound_method_eq_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestKeyErrorStrRegression()
    {
        // Regression: str(KeyError) must apply repr() to a single argument
        // (keeping quotes and type information) while no-arg and multi-arg
        // instances keep BaseException's form; subclasses inherit the
        // behavior unless they define their own __str__. Fails until the
        // fix lands.
        var module = RunModule("test_key_error_str_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFrozenSetHashRegression()
    {
        // Regression: the frozenset hash must be order-independent so that
        // equal frozensets hash equal, reproducing CPython's frozenset_hash
        // values for int-only sets; hash() must also preserve user __hash__
        // results within the Py_hash_t range exactly (with -1 mapped to -2).
        // Fails until the fix lands.
        var module = RunModule("test_frozenset_hash_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTruthinessLenRegression()
    {
        // Regression: truthiness must honor __len__ when __bool__ is absent
        // (bool/not/if/while/and-or were always truthy for __len__-only
        // types, including bytes, bytearray, range and memoryview);
        // exceptions from __len__ must propagate and invalid __len__ or
        // __bool__ returns must raise with CPython's messages. Fails until
        // the fix lands.
        var module = RunModule("test_truthiness_len_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBoolSlotVisibilityRegression()
    {
        // Regression: types without a real bool slot (object, str, list,
        // dict, set, tuple, frozenset, bytes, bytearray, memoryview, super,
        // type) must not expose __bool__ at all — their truthiness comes
        // from the dispatch-layer __len__ fallback (CPython PyObject_IsTrue).
        // Types with a real slot (int, float, bool, complex, NoneType,
        // NotImplementedType, range) keep __bool__.
        var module = RunModule("test_bool_slot_visibility_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDictReversedRegression()
    {
        // Regression: reversed() must support dicts and their views with
        // dedicated reverse iterators (dict_reverse*iterator type names),
        // instead of falling through to len/getitem and raising KeyError.
        // Mutation during iteration must still raise RuntimeError. Fails
        // until the fix lands.
        var module = RunModule("test_dict_reversed_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFormatSpecValidationRegression()
    {
        // Regression: format() must reject invalid presentation types and
        // flag/type combinations with CPython's messages: the z option is
        // refused for integers and clamps float negative zero, an omitted
        // float type renders like repr (or 'g'-with-add-dot-0 when a
        // precision is given), integer presentations reject a precision,
        // unknown single-character types report "Unknown format code", and
        // doubled grouping separators report "Cannot specify both". Fails
        // until the fix lands.
        var module = RunModule("test_format_spec_validation_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestZipNoArgsRegression()
    {
        // Regression: zip() with no arguments must be an exhausted
        // iterator (list(zip()) == [], next raises StopIteration), never
        // yielding the empty tuple once. Fails until the fix lands.
        var module = RunModule("test_zip_no_args_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestRaiseFromSuppressRegression()
    {
        // Regression: `raise X from Y` must set __suppress_context__ = True
        // for an exception cause as well as None (only `from None` did);
        // the implicit-context path stays False and a plain re-raise keeps
        // the exception untouched. Fails until the fix lands.
        var module = RunModule("test_raise_from_suppress_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestExceptionAttrSetterRegression()
    {
        // Regression: BaseException __cause__/__context__/__suppress_context__
        // must be writable like CPython: an exception assigned to __cause__
        // also suppresses the implicit context, None only clears, non-exception
        // values and deletes raise CPython's exact TypeErrors. Fails until the
        // fix lands.
        var module = RunModule("test_exception_attr_setter_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntUnicodeDigitsRegression()
    {
        // Regression: int() must accept any Unicode decimal digit (Nd) like
        // CPython, in base 10 and every other base, keeping CPython's exact
        // error messages (which quote the original string) and the digit-count
        // limit semantics. Fails until the fix lands.
        var module = RunModule("test_int_unicode_digits_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestErrorMessageSemanticsRegression()
    {
        // Regression: error messages aligned with CPython: super attribute
        // misses, subscript assignment/deletion on types without the slots,
        // and deleting through a data descriptor without __delete__. Fails
        // until the fix lands.
        var module = RunModule("test_error_message_semantics_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBytesIntConstructorRegression()
    {
        // Regression: bytes(n) with an index-able source (int, bool, any
        // __index__) must zero-fill n bytes like CPython, with CPython's
        // exact error handling for negative counts, ssize_t overflow and
        // replaced top-level TypeErrors. Fails until the fix lands.
        var module = RunModule("test_bytes_int_constructor_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGroupViewTypeRegression()
    {
        // Regression: ExceptionGroup reports non-exception items with their
        // 0-based index, dict view objects repr as content strings
        // (dict_keys([...]) etc.), and duplicate bases are rejected by
        // type() and class statements. Fails until the fix lands.
        var module = RunModule("test_group_view_type_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntRoundNdigitsRegression()
    {
        // Regression: round(int, ndigits) with a negative ndigits must
        // round to the nearest multiple of 10 ** -ndigits with ties going
        // to the even multiple (1250 -> 1200, -1350 -> -1400), ndigits must
        // go through __index__, and bool/int-subclass instances convert to
        // exact ints. Fails until the fix lands.
        var module = RunModule("test_int_round_ndigits_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStopIterationValueRegression()
    {
        // Regression: StopIteration must expose the value attribute like
        // CPython - a member initialized from args[0] (or None) at
        // construction, settable without touching args, deletable (reads
        // None afterwards, without falling back to args). Fails until the
        // fix lands.
        var module = RunModule("test_stopiteration_value_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSystemExitCodeRegression()
    {
        // Regression: SystemExit must expose the code attribute like CPython
        // - args[0] for one argument, the whole args tuple for multiple, None
        // for none; settable without touching args, deletable (reads None),
        // and absent on unrelated exception types. Fails until the fix lands.
        var module = RunModule("test_systemexit_code_regression.py");
        Assert.IsNotNull(module);
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
    public void TestOrdBytesRegression()
    {
        // Regression: ord() must accept one-byte bytes and bytearray objects
        // like CPython (returning the byte value), share the wrong-length
        // message across all three accepted types, keep the type-name
        // message for other types, and accept bytes subclasses. Fails until
        // the fix lands.
        var module = RunModule("test_ord_bytes_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestEncodeErrorsLazyRegression()
    {
        // Regression: encode() must resolve the errors handler name lazily
        // at the first actual encoding error like CPython - a successful
        // encoding never consults the name, and an unknown name raises
        // LookupError (not ValueError) with the CPython message. Fails
        // until the fix lands.
        var module = RunModule("test_encode_errors_lazy_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestReplDisplayHookRegression()
    {        // Regression: interactive top-level expression statements must echo
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
    public void TestStrFindCountRegression()
    {
        // Regression: the str search family must follow CPython's
        // ADJUST_INDICES (start clamps only at 0, so an above-range start
        // keeps the window negative) - partition raises on a missing
        // separator, an empty needle counts/finds at the zero-width window,
        // and expandtabs treats a negative tabsize as 0. Fails until the
        // fixes land.
        var module = RunModule("test_str_find_count_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFileIoErrorRegression()
    {
        // Regression: file IO must follow CPython - reopening the same path
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

        var module = RunModule("test_file_io_error_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatFloorDivInfRegression()
    {
        // Regression: float floor-division and divmod must derive their
        // results from the _float_div_mod fmod algorithm like CPython -
        // infinite operands yield nan and a zero quotient takes the true
        // quotient's sign (-2 // inf == -1.0). Fails until the fix lands.
        var module = RunModule("test_float_floordiv_inf_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBytesContainsAddRegression()
    {
        // Regression: bytes/bytearray __contains__ must treat bytes-like
        // operands (bytes, bytearray, memoryview) as subsequences in both
        // cross directions, treat index operands as byte membership with
        // the 0..255 validation, and reject the rest with the buffer
        // TypeError; bytes.__add__ accepts bytes-like operands and stays
        // bytes. Fails until the fixes land.
        var module = RunModule("test_bytes_contains_add_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrPercentFormatRegression()
    {
        // Regression: str % formatting must take '*' width/precision from
        // the argument tuple before the value itself, drive the mapping
        // lookup from %(name) keys in the format string (a dict without
        // keys formats as a single value), accept any number for %d/%i/%u
        // with floats truncating toward zero, and require __index__ for
        // %x/%X/%o with the conversion-specific TypeErrors.
        // Fails until the fixes land.
        var module = RunModule("test_str_percent_format_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFileSeekRegression()
    {
        // Regression: file.seek()/tell() must validate their arguments like
        // the CPython io stack - unknown whence values raise ValueError with
        // a per-layer message, offsets go through __index__ and the off_t
        // range, negative targets raise OSError EINVAL instead of leaking a
        // raw .NET exception, and the closed-file messages match the layer.
        // Fails until the fix lands.
        var module = RunModule("test_file_seek_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestExceptStarReraiseRegression()
    {
        // Regression: except* unwinding must follow PEP 654 - a bare raise
        // re-raises the matched subgroup (flattened together with the
        // unmatched rest when one exists), an exception escaping a handler
        // recombines with the rest instead of dropping it, a non-group
        // original propagates unwrapped when no handler matched, and a
        // bare raise without a live exception is a catchable RuntimeError.
        // Fails until the fixes land.
        var module = RunModule("test_except_star_reraise_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestExceptStarControlFlowSyntaxRegression()
    {
        // Regression: break/continue/return cannot cross an except* handler
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

    [TestMethod]
    public void TestSliceIndexRegression()
    {
        // Regression: slice bounds must go through __index__ like
        // PySlice_Unpack/_PyEval_SliceIndex - custom __index__ objects work
        // in every position, non-index objects raise the catchable slice
        // TypeError, zero step is rejected before the bounds unpack, huge
        // ints saturate, and the 32-bit length arithmetic must not overflow
        // on a saturated step. Fails until the fix lands.
        var module = RunModule("test_slice_index_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrCenterIsPrintableRegression()
    {
        // Regression: center must give the odd remainder column to the
        // left when both margin and width are odd (unicode_center_impl:
        // left = marg//2 + (marg & width & 1)), and isprintable must
        // reject the "Other"/"Separator" categories (Cc/Cf/Cs/Co/Cn and
        // Zs/Zl/Zp) with only the ASCII space printable. Fails until the
        // fixes land.
        var module = RunModule("test_str_center_isprintable_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSuperZeroArgsRegression()
    {
        // Regression: zero-argument super() must follow CPython
        // super_init_without_args - a catchable RuntimeError when the
        // calling frame has no positional parameters (module, plain
        // function, zero-parameter method) or arg[0] was deleted, never a
        // Debug.Assert on slot 0. Fails until the fix lands.
        var module = RunModule("test_super_zero_args_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatReprScientificRegression()
    {
        // Regression: float repr/str must render CPython's shortest
        // round-trip digits with lowercase 'e', two-digit exponents, and
        // the exact CPython scientific-notation thresholds (1e16 goes
        // scientific, 1e-05 goes scientific). Fails until the fix lands.
        var module = RunModule("test_float_repr_scientific_regression.py");
        Assert.IsNotNull(module);
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
    public void TestStdioNoBomRegression()
    {
        // Regression: the console hosts hand Console.OutputEncoding - possibly
        // the BOM-emitting UTF8Encoding singleton - to the stdio writers,
        // which prepended EF BB BF to every redirected run; CPython writes
        // no preamble. The redirected console host injects Encoding.UTF8 as
        // a deterministic stand-in and inherits the real builder path.
        var stdout = new MemoryStream();
        var stderr = new MemoryStream();
        var host = new RedirectedConsoleHost(new MemoryStream(), stdout, stderr);
        var filename = "test_stdio_no_bom_regression.py";
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
    public void TestFormatEmptySpecRegression()
    {
        // Regression: a zero-length format spec must make __format__
        // equivalent to str(obj). bool inherited int's Format slot and
        // rendered '1'/'0' in f-strings and format(); float rendered the
        // 'g' default-precision form instead of str(float)'s shortest
        // repr. Fails until the fix lands.
        var module = RunModule("test_format_empty_spec_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSearchBoundsSaturationRegression()
    {
        // Regression: str/list/tuple search-method start/end bounds
        // saturate oversized magnitudes into [0, len] like CPython's
        // Py_ssize_t conversion instead of raising an empty-message
        // OverflowError; a huge negative end clamps to 0 so the search
        // fails with the normal not-found ValueError.
        var module = RunModule("test_search_bounds_saturation_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestLenBoundsRegression()
    {
        // Regression: len() bounds the __len__ result like CPython's
        // slot_sq_length — any negative result is the >= 0 ValueError
        // and a positive result beyond the index range raises
        // OverflowError "cannot fit 'int' into an index-sized integer"
        // instead of being returned verbatim; a bool result is
        // normalized to an exact int.
        var module = RunModule("test_len_bounds_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntFormatZeroFlagAlignRegression()
    {
        // Regression: the '0' format-spec flag supplies fill '0' even
        // when an explicit alignment is present ("<04" ≡ "0<4"), and
        // only defaults the alignment to '=' when none was parsed;
        // previously the flag was ignored whenever an align existed,
        // silently falling back to space padding.
        var module = RunModule("test_int_format_zero_flag_align_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrSearchSliceIndexRegression()
    {
        // Regression: str search-method start/end bounds go through
        // CPython's _PyEval_SliceIndex — None keeps the default, objects
        // with __index__ convert (saturating for huge magnitudes), and
        // everything else raises the slice-indices TypeError instead of
        // being silently ignored; covers find/rfind/index/rindex/count/
        // startswith/endswith.
        var module = RunModule("test_str_search_slice_index_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSplitlinesKeependsTruthRegression()
    {
        // Regression: str.splitlines' keepends argument is a plain
        // truth test on the raw argument (CPython PyObject_IsTrue) —
        // int 1/2, floats and any other truthy object keep the line
        // endings; previously only bool was accepted so int truthy
        // values silently dropped the line endings.
        var module = RunModule("test_splitlines_keepends_truth_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComplexUnaryPowRegression()
    {
        // Regression: complex supports the unary -/+ slots (both
        // components negate; an exact +z returns itself) and the **
        // slot for complex-left operands — integer exponents,
        // fractional exponents via the polar principal branch, the
        // zero-exponent identity and the zero-base negative/complex
        // exponent ZeroDivisionError.
        var module = RunModule("test_complex_unary_pow_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGeneratorCloseStateThrow3Regression()
    {
        // Regression: a closed/exhausted generator stops iteration on
        // send (including non-None values) — the just-started TypeError
        // only applies to a never-started generator; and the deprecated
        // throw(type, value, tb) form warns with DeprecationWarning and
        // normalizes its arguments like CPython gen_throw instead of
        // being rejected.
        var module = RunModule("test_generator_close_state_throw3_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestPercentEmptyPrecisionRegression()
    {
        // Regression: %-formatting treats an empty precision field as
        // precision 0 (CPython sets the precision as soon as the dot is
        // consumed) instead of raising "format requires a precision";
        // a dot at the end of the spec stays an incomplete format.
        var module = RunModule("test_percent_empty_precision_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFormatReplacementIndexRegression()
    {
        // Regression: str.format's out-of-range positional field raises
        // CPython's "Replacement index N out of range for positional
        // args tuple" (carrying the field index) for both automatic and
        // manual numbering, instead of the generic "tuple index out of
        // range" .NET leak.
        var module = RunModule("test_format_replacement_index_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDataclassMutableDefaultRegression()
    {
        // Regression: @dataclass rejects mutable field defaults at
        // class definition time like CPython's _get_field — a default
        // whose type derives from list/dict/set/bytearray raises
        // "mutable default <class 'T'> for field <n> is not allowed:
        // use default_factory", while field(default_factory=...) and
        // hashable defaults stay accepted.
        var module = RunModule("test_dataclass_mutable_default_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntBase0LeadingZeroRegression()
    {
        // Regression: int(s, 0) follows the base-0 literal rules — a
        // prefix-less literal starting with '0' is invalid unless its
        // parsed value is zero (CPython long_from_string's
        // error_if_nonzero), so '08'/'0_8' raise ValueError while
        // '00'/'0_0' stay valid; explicit base 10 keeps accepting
        // leading zeros.
        var module = RunModule("test_int_base0_leading_zero_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDictViewLenContainsEqRegression()
    {
        // Regression: dict views implement the CPython view protocol —
        // sq_length reads the live source size, keys/items membership
        // looks up the source dict (dictkeys_contains/dictitems_contains),
        // and the set-like richcompare (dictview_richcompare) compares
        // contents against sets and other set-like views, gated on the
        // length check; dict_values keeps iteration-only membership and
        // identity-only equality.
        var module = RunModule("test_dict_view_len_contains_eq_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBinopErrorMessagesRegression()
    {
        // Regression: binary-operator TypeErrors use CPython's message
        // templates — "unsupported operand type(s) for OP:" with the
        // "** or pow()" display and augmented in-place names ("+=", "**="),
        // the per-type concatenate errors raised by the left operand's
        // sq_concat after reflection declines, and _PySequence_IterSearch's
        // "argument of type ... is not a container or iterable" for `in`.
        var module = RunModule("test_binop_error_messages_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestGeneratorThrowUnstartedRegression()
    {
        // Regression: gen_throw on a never-started generator propagates
        // the exception straight to the caller and closes the generator
        // instead of resuming the VM (which popped an empty operand stack
        // and crashed the process with IndexOutOfRangeException).
        var module = RunModule("test_generator_throw_unstarted_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBigintFloatOverflowRegression()
    {
        // Regression: arithmetic conversions of a bigint to float follow
        // PyLong_AsDouble — values outside the double range (including
        // ones rounding up to infinity) raise "int too large to convert
        // to float" in mixed int/float and int/complex operations,
        // negative-exponent int pow, the complex() constructor and the
        // %e/%f/%g/% format fallback; comparisons never convert.
        var module = RunModule("test_bigint_float_overflow_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestNegBaseFractionalPowRegression()
    {
        // Regression: float_pow's negative-base rule — a finite negative
        // base with a non-integral exponent evaluates the complex
        // principal value via complex_pow instead of returning float nan;
        // the integral/zero/inf/NaN special cases keep float results, and
        // int/int true division rounds the quotient half-to-even like
        // CPython's long_true_divide so fractional exponents stay exact.
        var module = RunModule("test_neg_base_fractional_pow_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatFromhexOptionalExponentRegression()
    {
        // Regression: float.fromhex implements CPython's float_fromhex —
        // the 0x prefix, fraction, (p|P) exponent and surrounding
        // whitespace are optional (missing exponent means p0, 'e' stays
        // a hex digit), the coefficient rounds half-to-even with the
        // exponent applied as powers of two, huge exponents clamp to the
        // overflow/underflow paths, and non-str arguments report the
        // built-in conversion TypeError.
        var module = RunModule("test_float_fromhex_optional_exponent_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatFromhexNonfiniteRegression()
    {
        // Regression: float.fromhex parses non-finite literals like
        // CPython's _Py_parse_inf_or_nan — an optionally signed
        // inf/infinity/nan token, ASCII case-insensitive, checked before
        // the hex grammar; partial tokens still fail the literal check,
        // keeping the hex() round trip intact for inf and NaN.
        var module = RunModule("test_float_fromhex_nonfinite_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatFormatPercentNonfiniteRegression()
    {
        // Regression: the '%' presentation type appends its suffix to
        // non-finite floats too — CPython adds '%' unconditionally after
        // rendering inf/nan, and the suffix participates in width, fill
        // and alignment; precision and grouping stay no-ops there.
        var module = RunModule("test_float_format_percent_nonfinite_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComplexStringParsingRegression()
    {
        // Regression: complex(str) parses string arguments like CPython's
        // complex_from_string_inner — a '_' pre-pass allowing underscores
        // only between digits, then <float>, <float>j,
        // <float><signed-float>j plus the legacy <sign>j/j forms, an
        // optional repr bracket, inf/nan literals and the dedicated
        // ValueError messages for malformed input.
        var module = RunModule("test_complex_string_parsing_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatAsIntegerRatioSpecialRegression()
    {
        // Regression: float.as_integer_ratio raises OverflowError for
        // infinities and ValueError for NaN, with CPython's two dedicated
        // messages; finite values keep the exact reduced pair.
        var module = RunModule("test_float_as_integer_ratio_special_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatFromhexSubnormalRegression()
    {
        // Regression: float.fromhex parses subnormal hex strings with IEEE
        // 754 subnormal semantics — values below the normalized range keep
        // their magnitude and sign down to 5e-324, and the hex→fromhex
        // round trip stays an exact identity across the subnormal range.
        var module = RunModule("test_float_fromhex_subnormal_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatRoundSubnormalRegression()
    {
        // Regression: round(x, ndigits) keeps subnormal magnitudes when
        // ndigits matches the value's decimal order (the 10**nd composition
        // used to overflow the double range and collapse to -0.0), and a
        // coarse-place rounding that overflows raises OverflowError with
        // CPython's double_round message.
        var module = RunModule("test_float_round_subnormal_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMathLogIntParityRegression()
    {
        // Regression: math.log10/log2/log on ints mirror CPython's
        // loghelper — the value converts to double for the correctly
        // rounded libm call (exact results for powers of ten), huge ints
        // fall back to _PyLong_Frexp scaling, and domain errors carry the
        // original argument repr.
        var module = RunModule("test_math_log_int_parity_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBytearrayOrderingRegression()
    {
        // Regression: bytearray order comparisons accept every bytes-like
        // operand and compare bytewise with a length tiebreak; bytes
        // reaches them through the reflected slot while its own order
        // comparisons still require two exact bytes operands, and bytes
        // gains the previously missing <=/>/>= slots.
        var module = RunModule("test_bytearray_ordering_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBytesStrEncodingRegression()
    {
        // Regression: bytes(str, encoding, errors) encodes through the
        // str.encode core (bytearray too), with the clinic faces in
        // CPython order — arity, keyword names, str type checks, the
        // without-a-string guards — plus the latin-1/utf8/mbcs/mac-roman/
        // cpNNNN codec alias families and strict encode converters.
        var module = RunModule("test_bytes_str_encoding_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestUnaryNotimplementedRegression()
    {
        // Regression: unary slot hooks pass their result through unchecked
        // — __neg__/__pos__/__invert__ returning NotImplemented yield the
        // singleton itself like __abs__ always did, while a missing slot
        // still raises the bad-operand TypeError and the binary reflected
        // fallback keeps its own semantics.
        var module = RunModule("test_unary_notimplemented_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSliceValueSemanticsRegression()
    {
        // Regression: slice objects carry value semantics — rich comparison
        // delegates to the (start, stop, step) tuple with the identity
        // shortcut, the hash is the tuplehash lanes without the length
        // mix-in, repr prints the three-part constructor form, and
        // slice.indices() converts like CPython's METH_O binding.
        var module = RunModule("test_slice_value_semantics_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTupleStrNoargRegression()
    {
        // Regression: tuple() and str() construct without arguments and
        // their dispatch reports CPython's exact arity/keyword faces; str
        // binds object/encoding/errors by keyword and decodes bytes-like
        // sources, sharing bytes.decode's codec core.
        var module = RunModule("test_tuple_str_noarg_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestModuleLazyAttrsRegression()
    {
        // Regression: PEP 562 module-level __getattr__/__dir__ hooks — a
        // namespace miss calls the module's __getattr__ (exceptions
        // propagate unchanged, an AttributeError counting as missing), and
        // the module's __dir__ honors a module-dict __dir__ hook with
        // dir() sorting the result.
        var module = RunModule("test_module_lazy_attrs_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestQualnameMetadataRegression()
    {
        // Regression: qualified names and creation metadata — class
        // __qualname__ carries the lexical scope path, the class-body
        // preamble seeds __module__/__qualname__/__firstlineno__ (with
        // __qualname__ moving to the type accessor), and functions,
        // lambdas, generators, and coroutines expose
        // __qualname__/__module__ with CPython's read/write behavior.
        var module = RunModule("test_qualname_metadata_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestSortComparisonSemanticsRegression()
    {
        // Regression: sort comparisons are PyObject_RichCompareBool(pivot,
        // placed, Py_LT) with the result interpreted by Python truthiness —
        // non-bool __lt__ return values (truthy tuples, None, ints) sort
        // like CPython's listsort, whose count_run/binarysort comparison
        // order is ported so an asymmetric __lt__ permutes identically.
        var module = RunModule("test_sort_comparison_semantics_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBytesDecodeErrorsRegression()
    {
        // Regression: bytes.decode defaults to errors='strict' and raises
        // UnicodeDecodeError (carrying encoding/object/start/end/reason with
        // CPython's str() forms) on invalid input instead of silently
        // replacing, and the errors= parameter works in keyword and
        // positional form with the standard handlers.
        var module = RunModule("test_bytes_decode_errors_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestContainerInitRegression()
    {
        // Regression: dict/list/set subclass construction dispatches to a
        // custom __init__ (which replaces the built-in tp_init) instead of
        // iterating the first argument as the builtins do; tuple/frozenset
        // consume the iterable in tp_new and then still call the subclass
        // __init__, and __new__/__init__ resolve by MRO rather than
        // class-creation order.
        var module = RunModule("test_container_init_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrErrorMessagesRegression()
    {
        // Regression: str method TypeErrors use CPython's exact wording
        // with the argument ordinal and offending type name, the numeric
        // converter arguments (maxsplit/count/width/tabsize) convert
        // through __index__ with the C-level OverflowError messages, and
        // index/rindex carry their own templates.
        var module = RunModule("test_str_error_messages_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAssertTupleWarningRegression()
    {
        // Regression: an assert whose test is a non-empty tuple literal
        // display emits CPython's compile-time SyntaxWarning ("assertion
        // is always true, perhaps remove parentheses?") while the runtime
        // behavior is unchanged; variable-bound tuples, parenthesized
        // non-tuples, and the always-false empty tuple stay silent.
        var module = RunModule("test_assert_tuple_warning_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDocstringDedentRegression()
    {
        // Regression: __doc__ (function/class/module first-statement
        // string literals, exec included) is cleaned at compile time with
        // CPython's _PyCompile_CleanDoc semantics — expandtabs, first-line
        // leading-space strip, and common-margin dedent of the following
        // non-blank lines — while non-docstring constants stay raw.
        var module = RunModule("test_docstring_dedent_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDataclassGeneratorRegression()
    {
        // Regression: the dataclass generator merges inherited base
        // fields into the generated __init__, supports field(metadata=)
        // and kw_only (class and field level), calls __post_init__ with
        // the InitVar values, keeps InitVar pseudo-fields out of the
        // instance, and generates __match_args__ for class patterns.
        var module = RunModule("test_dataclass_generator_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestAcceptableBaseRegression()
    {
        // Regression: range/slice are sealed static types, so subclass
        // creation raises "type 'X' is not an acceptable base type" via
        // type() and metaclass paths alike, with per-base sealed/layout
        // checks winning over the later duplicate-base scan.
        var module = RunModule("test_acceptable_base_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestRedundantBaseLayoutRegression()
    {
        // Regression: a base whose layout is already covered by another
        // base's MRO (K(B, object), K(D, dict)) must not raise a layout
        // conflict; only unrelated layouts conflict, and unlinearizable
        // orders fail with "Cannot create a consistent method resolution
        // order (MRO) for bases X, Y".
        var module = RunModule("test_redundant_base_layout_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestExceptionMixinInheritanceRegression()
    {
        // Regression: exception classes combined with plain mixin bases
        // (in either base order) define, instantiate, raise and match
        // except single-class, tuple and except* handlers along the full
        // MRO; only unrelated solid layouts (Exception + dict) keep
        // conflicting and non-exception handlers stay refused.
        var module = RunModule("test_exception_mixin_inheritance_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClassBodyGlobalRegression()
    {
        // Regression: `global` declarations inside a class body route every
        // binding form (assignment, walrus, import-as, def/class, for/with/
        // except-as targets) to the module globals instead of the class
        // dict, and an annotated name combined with global/nonlocal is a
        // compile error in any order outside the module-level
        // global-before-annotation case.
        var module = RunModule("test_class_body_global_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestPowSlotArityRegression()
    {
        // Regression: the pow-family slots pass two arguments to Python-level
        // __pow__/__rpow__ for the binary form (modulo None) and three only
        // for the ternary pow() form, matching slot_nb_power.
        var module = RunModule("test_pow_slot_arity_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIPowProtocolRegression()
    {
        // Regression: **= dispatches to __ipow__ with two arguments (the
        // modulus is dropped) and falls back to __pow__ when __ipow__ is
        // not defined, matching slot_nb_inplace_power.
        var module = RunModule("test_ipow_protocol_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestTypeAttrGuardRegression()
    {
        // Regression: attribute set/delete on static (non-heap) types
        // raises "cannot set 'X' attribute of immutable type 'Y'" (the
        // delete path reports "cannot set" too) while heap classes,
        // custom metaclasses, and function instances keep full support.
        var module = RunModule("test_type_attr_guard_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestLengthHintRegression()
    {
        // Regression: the length-hint protocol. len() wins (only its
        // TypeError falls through), __length_hint__ is looked up on the
        // type MRO and only its TypeError falls back — every other error
        // propagates out of list()/extend/+=/[*x]/sorted()/bytes()/
        // bytearray.extend/operator.length_hint, with value validation
        // (TypeError/ValueError/OverflowError/MemoryError). tuple/set/dict
        // construction, dict.update, set.update, the bytearray()
        // constructor, and bytearray += stay hint-free.
        var module = RunModule("test_length_hint_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBoolOpNotConditionRegression()
    {
        // Regression: `not` as the last operand of a short-circuiting test
        // must not be folded into the condition jump. A bool short-circuit
        // value used to invert the branch, and any other truthy short-circuit
        // value used to reach the jump un-converted (InvalidCastException).
        // Covers if/while/assert/match guard/ternary/comprehension,
        // including the async comprehension if clause.
        var module = RunModule("test_boolop_not_condition_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestIntTruedivPrecisionRegression()
    {
        // Regression: int/int true division follows CPython's
        // long_true_divide exactly — the shift clamps at DBL_MIN_EXP so
        // subnormal quotients round once in the integer domain, 0/negative
        // keeps a -0.0 sign, underflow below 2^-1075 lands on signed zero,
        // and a quotient rounding up to 2^1024 raises OverflowError
        // instead of returning inf; half-even ties and exact quotients
        // stay correctly rounded.
        var module = RunModule("test_int_truediv_precision_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestStrSubclassRegression()
    {
        // Regression: str subclass instances keep their subclass type —
        // construction hands out a fresh retagged copy (unicode_subtype_new)
        // instead of a shared exact str, so subclass methods resolve,
        // overrides win, instance dicts work and str() converts back to the
        // exact type; bytes/float/tuple copy for subtypes too instead of
        // retagging shared instances (source objects, empty singletons)
        // process-wide, and exact-type construction keeps identity
        // passthrough.
        var module = RunModule("test_str_subclass_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComprehensionScopeRegression()
    {
        // Regression: list/set/dict comprehensions compile with their own
        // implicit scope in every enclosing scope kind (function, module,
        // class) — iteration variables never leak, never appear out of thin
        // air, and never rebind same-named locals or closure cells; walrus
        // targets inside a comprehension bind in the enclosing scope (PEP
        // 572), eval/exec sees comprehension targets together with the
        // owner function's locals, and async comprehensions still require
        // an async function.
        var module = RunModule("test_comprehension_scope_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMetaclassPrepareRegression()
    {
        // Regression: the metaclass __prepare__ hook (PEP 3115) runs before
        // the class body and its return value becomes the namespace the
        // body executes in — the same object then reaches the metaclass
        // __new__/__init__. The hook resolves through a full attribute
        // lookup on the metaclass (inherited type.__prepare__ included, so
        // hasattr(type, "__prepare__") holds), the most derived metaclass's
        // hook wins, class kwargs pass through, raising hooks abort class
        // creation, non-mapping returns raise TypeError, and non-dict
        // mappings run the body before type.__new__ rejects them
        // (dict subclasses with overridden __setitem__ see every store).
        var module = RunModule("test_metaclass_prepare_regression.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFloatReprShortestRegression()
    {
        // Regression: float repr renders the shortest round-trip digit
        // string per CPython dtoa mode 0 using exact BigInteger math —
        // .NET's shortest mode emits one digit too few on ties like
        // 2**-25 (its 16-digit output parses back to the lower neighbor);
        // presentation boundaries (scientific thresholds, integral ".0")
        // and the power-of-two quarter-ulp cell floor stay aligned.
        var module = RunModule("test_float_repr_shortest_regression.py");
        Assert.IsNotNull(module);
    }
}

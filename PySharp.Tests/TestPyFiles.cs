using PySharp.Modules.Builtins;
using PySharp.Runtime;

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
    public void TestOpen()
    {
        var module = RunModule("test_open.py");
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestListBugs()
    {
        var module = RunModule("test_list_bugs.py");
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

}

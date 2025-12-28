using PySharp.PyRuntime;

namespace PySharp.Tests;

[TestClass]
public sealed class TestPyFiles
{
    private const string PyFilesPath = "test_pyfiles";

    [TestMethod]
    public void TestClassSimple()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_class_simple.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClassInherit()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_class_inherit.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClassSpecialMethods()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_class_special_methods.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestUserDefinedDescriptor()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_user_defined_descriptor.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestMethodDescriptor()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_method_descriptor.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDecorator()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_decorator.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestFString()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_fstring.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestOperators()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_operators.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestProperty()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_property.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestYield()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_yield.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestList()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_list.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestDict()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_dict.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestBuiltinFuncs()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_builtin_funcs.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestClosure()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_closure.py"));
        Assert.IsNotNull(module);
    }

    [TestMethod]
    public void TestComprehension()
    {
        var module = PyInterpreter.RunFile(Path.Combine(PyFilesPath, "test_comprehension.py"));
        Assert.IsNotNull(module);
    }
}

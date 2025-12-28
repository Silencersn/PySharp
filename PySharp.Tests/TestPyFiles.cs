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
}

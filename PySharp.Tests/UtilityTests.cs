using PySharp.Modules.Builtins;
using PySharp.Runtime.Comparison;
using PySharp.Runtime.IO.Memory;
using PySharp.Utility;

#pragma warning disable MSTEST0037

namespace PySharp.Tests;

[TestClass]
public sealed class UtilityTests
{
    private static void WriteText(MemoryFileSystem fs, string path, string text)
    {
        using var s = fs.GetFile(path).Open(FileMode.Create, FileAccess.Write, FileShare.None);
        using var w = new StreamWriter(s);
        w.Write(text);
    }

    private static string ReadText(MemoryFileSystem fs, string path)
    {
        using var s = fs.GetFile(path).Open(FileMode.Open, FileAccess.Read, FileShare.None);
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    // ===== ConcurrentSet<T> =====

    [TestMethod]
    public void ConcurrentSet_AddAndContains()
    {
        var set = new ConcurrentSet<int>();
        Assert.IsTrue(set.Add(1));
        Assert.IsFalse(set.Add(1));
        Assert.IsTrue(set.Contains(1));
        Assert.IsFalse(set.Contains(99));
    }

    [TestMethod]
    public void ConcurrentSet_Remove()
    {
        var set = new ConcurrentSet<string>();
        set.Add("hello");
        Assert.IsTrue(set.Remove("hello"));
        Assert.IsFalse(set.Contains("hello"));
    }

    [TestMethod]
    public void ConcurrentSet_Clear()
    {
        var set = new ConcurrentSet<int>();
        set.Add(1);
        set.Clear();
        Assert.IsTrue(set.Count() == 0);
    }

    // ===== StringKeyDict =====

    [TestMethod]
    public void StringKeyDict_Basic()
    {
        PyDictObject pyDict = [];
        IDictionary<string, PyObject> dict = pyDict;
        dict.Add("k", PyIntObject.FromInteger(42));
        Assert.IsTrue(dict.ContainsKey("k"));
        Assert.IsTrue(dict.TryGetValue("k", out var v));
        Assert.IsTrue(((PyIntObject)v!).Value == 42);
        dict["k"] = PyIntObject.FromInteger(100);
        Assert.IsTrue(((PyIntObject)dict["k"]).Value == 100);
    }

    [TestMethod]
    public void StringKeyDict_Count()
    {
        PyDictObject pyDict = [];
        IDictionary<string, PyObject> dict = pyDict;
        Assert.IsTrue(dict.Count == 0);
        dict.Add("a", PyNoneObject.None);
        Assert.IsTrue(dict.Count == 1);
    }

    // ===== DictAdapter =====

    [TestMethod]
    public void DictAdapter_StringKey()
    {
        var orig = new Dictionary<string, PyObject?>();
        IDictionary<PyObject, PyObject> adapter = new DictAdapter(orig);
        adapter.Add(PyStrObject.FromString("k"), PyIntObject.FromInteger(42));
        Assert.IsTrue(adapter.ContainsKey(PyStrObject.FromString("k")));
    }

    [TestMethod]
    public void DictAdapter_ExtraKey()
    {
        var orig = new Dictionary<string, PyObject?>();
        IDictionary<PyObject, PyObject> adapter = new DictAdapter(orig);
        var ik = PyIntObject.FromInteger(99);
        adapter[ik] = PyStrObject.FromString("v");
        Assert.IsTrue(((PyStrObject)adapter[ik]).Value == "v");
    }

    // ===== PyObjectComparer =====

    [TestMethod]
    public void PyObjectComparer_Compare()
    {
        var c = PyObjectComparer.Default;
        var a = PyIntObject.FromInteger(5);
        var b = PyIntObject.FromInteger(10);
        Assert.IsTrue(c.Compare(a, b) < 0);
        Assert.IsTrue(c.Compare(b, a) > 0);
        Assert.IsTrue(c.Compare(a, a) == 0);
    }

    [TestMethod]
    public void PyObjectComparer_Null()
    {
        var c = PyObjectComparer.Default;
        var o = PyIntObject.FromInteger(1);
        Assert.IsTrue(c.Compare(null, o) < 0);
        Assert.IsTrue(c.Compare(o, null) > 0);
        Assert.IsTrue(c.Compare(null, null) == 0);
    }

    [TestMethod]
    public void PyObjectComparer_HashCode()
    {
        var c = PyObjectComparer.Default;
        var a = PyIntObject.FromInteger(42);
        var b = PyIntObject.FromInteger(42);
        Assert.IsTrue(c.GetHashCode(a) == c.GetHashCode(b));
    }

    // ===== MemoryFileSystem =====

    [TestMethod]
    public void MemoryFileSystem_CreateDirectories()
    {
        var fs = new MemoryFileSystem("C:\\");
        var d = fs.GetDirectory("C:\\a\\b");
        Assert.IsFalse(d.Exists);
        d.Create();
        Assert.IsTrue(d.Exists);
        Assert.IsTrue(fs.GetDirectory("C:\\a").Exists);
    }

    [TestMethod]
    public void MemoryFileSystem_DeleteDirectory()
    {
        var fs = new MemoryFileSystem("C:\\");
        fs.GetDirectory("C:\\d").Create();
        Assert.IsTrue(fs.GetDirectory("C:\\d").Exists);
        fs.GetDirectory("C:\\d").Delete();
        Assert.IsFalse(fs.GetDirectory("C:\\d").Exists);
    }

    [TestMethod]
    public void MemoryFileSystem_ReadWriteFile()
    {
        var fs = new MemoryFileSystem("C:\\");
        WriteText(fs, "C:\\f.txt", "hello");
        Assert.IsTrue(fs.ExistsFile("C:\\f.txt"));
        Assert.IsTrue(ReadText(fs, "C:\\f.txt") == "hello");
    }

    [TestMethod]
    public void MemoryFileSystem_DeleteFile()
    {
        var fs = new MemoryFileSystem("C:\\");
        WriteText(fs, "C:\\f.txt", "temp");
        fs.GetFile("C:\\f.txt").Delete();
        Assert.IsFalse(fs.ExistsFile("C:\\f.txt"));
    }

    [TestMethod]
    public void MemoryFileSystem_Enumerate()
    {
        var fs = new MemoryFileSystem("C:\\");
        WriteText(fs, "C:\\a.txt", "");
        WriteText(fs, "C:\\b.txt", "");
        fs.GetDirectory("C:\\sub").Create();
        Assert.IsTrue(fs.GetDirectory("C:\\").EnumerateFiles().Count() == 2);
        Assert.IsTrue(fs.GetDirectory("C:\\").EnumerateDirectories().Count() == 1);
    }
}

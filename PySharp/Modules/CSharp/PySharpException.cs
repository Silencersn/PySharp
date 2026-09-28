using PySharp.Modules.Builtins;
using PySharp.Runtime.PyAttributes;

namespace PySharp.Modules.CSharp;

/// <summary>
/// PySharp-proprietary exception type raised deliberately into Python at
/// explicitly marked not-implemented / not-supported boundaries.
/// <para/>
/// The instance is a first-class Python exception rooted at <c>BaseException</c>
/// and rendered in Python traceback format, so the gap surfaces where it occurs
/// and Python-level code can catch and handle it instead of forcing a C#-side
/// change. The raise sites double as an in-source registry of known behavioral
/// gaps, enumerable by searching this type's factory calls; truly unreachable
/// paths use <see cref="System.UnreachableException"/> instead.
/// <para/>
/// The name is deliberately not bound in any Python-visible namespace: PySharp
/// exposes no proprietary module, and registering the type in builtins or any
/// standard module would inject a CPython-nonexistent name into compatible
/// semantics. Python code therefore cannot catch it by name (<c>NameError</c>);
/// handling is a broad <c>except</c> plus type-name inspection.
/// </summary>
[PyException("PySharpException", Bases = [], IsSealed = true)]
[PyTypeConstructor(AccessModifier = "internal")]
internal sealed partial class PySharpException : PyExceptionType;
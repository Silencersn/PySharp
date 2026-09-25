using System.Collections.Generic;

namespace PySharp.Runtime;

/// <summary>
/// The CPython tp_flags bits PySharp models, keeping the CPython bit values.
/// Py_TPFLAGS_SEQUENCE (1 &lt;&lt; 5) and Py_TPFLAGS_MAPPING (1 &lt;&lt; 6) drive the
/// match statement's MATCH_SEQUENCE/MATCH_MAPPING opcodes — pure flag checks
/// (Python/bytecodes.c), unrelated to slot presence: str/bytes/bytearray have
/// sq_item yet must not match sequence patterns, and a plain class defining
/// __getitem__ must not either. _Py_TPFLAGS_MATCH_SELF (1 &lt;&lt; 22) lets a class
/// without __match_args__ accept one positional sub-pattern matching the
/// subject itself.
/// </summary>
[Flags]
internal enum PyTypeFlags
{
    None = 0,
    Sequence = 1 << 5,
    Mapping = 1 << 6,
    MatchSelf = 1 << 22,

    /// <summary>Py_TPFLAGS_SEQUENCE | Py_TPFLAGS_MAPPING — mutually exclusive (typeobject.c COLLECTION_FLAGS).</summary>
    CollectionMask = Sequence | Mapping,
}

internal static class PyTypeFlagsTable
{
    // the analog of the hand-written tp_flags in each Objects/xxxobject.c:
    // CPython sets these bits statically, never derives them from slots.
    // bool picks MATCH_SELF up from int through the MRO walk.
    private static readonly Dictionary<string, PyTypeFlags> Table = new()
    {
        ["list"] = PyTypeFlags.Sequence | PyTypeFlags.MatchSelf,
        ["tuple"] = PyTypeFlags.Sequence | PyTypeFlags.MatchSelf,
        ["range"] = PyTypeFlags.Sequence,
        ["memoryview"] = PyTypeFlags.Sequence,
        ["dict"] = PyTypeFlags.Mapping | PyTypeFlags.MatchSelf,
        ["int"] = PyTypeFlags.MatchSelf,
        ["float"] = PyTypeFlags.MatchSelf,
        ["complex"] = PyTypeFlags.MatchSelf,
        ["str"] = PyTypeFlags.MatchSelf,
        ["bytes"] = PyTypeFlags.MatchSelf,
        ["bytearray"] = PyTypeFlags.MatchSelf,
        ["set"] = PyTypeFlags.MatchSelf,
        ["frozenset"] = PyTypeFlags.MatchSelf,
    };

    internal static PyTypeFlags Lookup(string tpName) =>
        Table.TryGetValue(tpName, out PyTypeFlags flags) ? flags : PyTypeFlags.None;
}

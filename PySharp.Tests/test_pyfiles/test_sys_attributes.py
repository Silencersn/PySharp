"""Verifies the sys module's standard surface end to end: the live sys.path list driving the import machinery, the sys.modules registry with delete/substitute/None-halt semantics, sys.exit's SystemExit channel, the recursion limit pair, constants, intern, the struct sequences version_info/flags, and the implementation namespace.

:kind: test
"""

# test_sys_attributes: sys module standard attributes

import sys


def expect(exc_type, func, *args):
    try:
        func(*args)
        return "no exception"
    except exc_type as e:
        return str(e)


# --- sys.path --------------------------------------------------------------

print("path_type:", type(sys.path))
print("path_member:", hasattr(sys.path, "append") and hasattr(sys.path, "insert"))
sys.path.append("nowhere_at_all")
try:
    import definitely_missing_module
except ModuleNotFoundError:
    print("path_drives_import: ok")
sys.path.remove("nowhere_at_all")
print("path_back:", "nowhere_at_all" not in sys.path)


# --- sys.modules ------------------------------------------------------------

import math
print("modules_type:", type(sys.modules))
print("modules_hit:", "math" in sys.modules)
del sys.modules["math"]
print("modules_deleted:", "math" not in sys.modules)
import math
print("modules_reloaded:", "math" in sys.modules)
sys.modules["math"] = None
try:
    import math
    print("halted: no")
except ImportError as e:
    print("halted:", e)
print("modules_none_kept:", sys.modules["math"] is None)


# --- sys.exit ---------------------------------------------------------------

try:
    sys.exit(3)
except SystemExit as e:
    print("exit_code:", e.code)
try:
    sys.exit("bye")
except SystemExit as e:
    print("exit_str:", e.code)
try:
    sys.exit()
except SystemExit as e:
    print("exit_none:", e.code)


# --- recursion limit --------------------------------------------------------

print("limit_default:", sys.getrecursionlimit())
sys.setrecursionlimit(2000)
print("limit_set:", sys.getrecursionlimit())
sys.setrecursionlimit(1000)
print("limit_low:", expect(ValueError, sys.setrecursionlimit, 0))
print("limit_type:", expect(TypeError, sys.setrecursionlimit, "x"))


# --- constants ---------------------------------------------------------------

print("platform:", sys.platform == "win32")
print("byteorder:", sys.byteorder == "little")
print("maxsize:", sys.maxsize == 2**63 - 1)
print("maxunicode:", sys.maxunicode == 1114111)
print("dont_write_bytecode:", sys.dont_write_bytecode is True)
print("defaultencoding:", sys.getdefaultencoding())
print("exec_is_str:", isinstance(sys.executable, str))
print("prefix_is_str:", isinstance(sys.prefix, str))
print("argv_is_list:", isinstance(sys.argv, list))
print("builtins_sorted:", sys.builtin_module_names == tuple(sorted(sys.builtin_module_names)))
print("builtins_has_sys:", "sys" in sys.builtin_module_names)


# --- intern -------------------------------------------------------------------

s = sys.intern("interned_probe_string")
print("intern_value:", s)
t = "another_unique_intern_token"
print("intern_same:", sys.intern(t) is t)
print("intern_type:", type(sys.intern("plain")) is str)
print("intern_nonstr:", expect(TypeError, sys.intern, 5))


class StrSub(str):
    pass


print("intern_sub:", expect(TypeError, sys.intern, StrSub("q")))


# --- version_info / flags -----------------------------------------------------

print("vi_type:", type(sys.version_info).__name__)
print("vi_ctor:", expect(TypeError, type(sys.version_info), 5))
print("vi_shape:", sys.version_info.major == 3 and sys.version_info.minor == 14 and sys.version_info.releaselevel == "final" and sys.version_info.serial == 0)
print("vi_index:", sys.version_info[0] == 3 and sys.version_info[3] == "final")
# the -B reference run and PySharp agree on the first 13 int fields
# (debug..isolated); utf8_mode tracks the host's PYTHONUTF8, so the repr
# is environment-dependent and stays out of the corpus
print("flags_head:", sys.flags[:13] == (0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0))
print("flags_bool:", sys.flags.dev_mode is False and sys.flags.safe_path is False)
print("flags_wde:", sys.flags.warn_default_encoding == 0)
sys.set_int_max_str_digits(1000)
print("flags_live:", sys.flags.int_max_str_digits == 1000)
sys.set_int_max_str_digits(4300)


# --- implementation -----------------------------------------------------------

impl = sys.implementation
print("impl_name:", impl.name)
print("impl_tag:", impl.cache_tag)
print("impl_version:", impl.version == sys.version_info)
print("impl_hexversion:", impl.hexversion == (3 << 24) | (14 << 16) | (sys.version_info.micro << 8) | 0xF0)
print("impl_isolated:", impl.supports_isolated_interpreters)
print("impl_type:", type(impl).__name__)
print("impl_type_repr:", repr(type(impl)))
print("impl_type_module:", type(impl).__module__)
NS = type(impl)
print("ns_ctor:", NS(a=1, b="x"))
print("ns_repr:", NS(p=9))
print("ns_too_many:", expect(TypeError, NS, 1, 2))
print("ns_nonstr_key:", expect(TypeError, NS, {5: 1}))

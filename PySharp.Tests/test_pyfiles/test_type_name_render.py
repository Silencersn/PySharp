"""Error messages render the CPython tp_name (bare __name__) for runtime-created classes instead of the qualname, except the property wording (qualname), the dict-key/set-element %T wording (fully qualified name), and exception/bytearray reprs (bare name via _PyType_Name).

:kind: test
"""

# Regression: the type name inside an error message is CPython's tp_name, not
# the __qualname__ PySharp used to pass. A class created at runtime is named by
# its bare __name__ whatever its __qualname__ or __module__ say
# (Objects/typeobject.c type_new_set_name, then every message that reads
# Py_TYPE(x)->tp_name: Objects/object.c, call.c, abstract.c), so a function
# local or nested class must not leak "<locals>" or a module prefix. The two
# deliberate exceptions stay put: the property wording uses the qualname
# (Objects/descrobject.c reads PyType_GetQualName) and the dict-key/set-element
# hash failure uses the fully qualified name (%T,
# Objects/typeobject.c _PyType_GetFullyQualifiedName). An exception repr names
# the type with _PyType_Name (Objects/exceptions.c BaseException_repr), i.e.
# the bare name as well.

import test_type_name_render_helper as helper


def expect(label, fn, exc, msg):
    try:
        fn()
    except exc as e:
        assert str(e) == msg, f"{label}: {str(e)!r} != {msg!r}"
    else:
        assert False, f"{label}: no {exc.__name__}"


def factory():
    class LocalClass:
        # explicit unhashability so the dict-key/set-element wording is reachable
        __hash__ = None

        def __iter__(self):
            return 1

    return LocalClass


class Outer:
    class Nested:
        pass


LocalClass = factory()
LocalFromModule = helper.make()

# --- tp_name: a runtime-created class renders bare, in every message family
expect("attr.local", lambda: LocalClass().nope, AttributeError,
       "'LocalClass' object has no attribute 'nope'")
expect("attr.nested", lambda: Outer.Nested().nope, AttributeError,
       "'Nested' object has no attribute 'nope'")
expect("attr.crossmodule", lambda: LocalFromModule().nope, AttributeError,
       "'LocalClass' object has no attribute 'nope'")
expect("call.local", lambda: LocalClass()(), TypeError,
       "'LocalClass' object is not callable")
expect("len.local", lambda: len(LocalClass()), TypeError,
       "object of type 'LocalClass' has no len()")
expect("subscript.local", lambda: LocalClass()[0], TypeError,
       "'LocalClass' object is not subscriptable")
expect("type.subscript", lambda: LocalClass[0], TypeError,
       "type 'LocalClass' is not subscriptable")
expect("iter.noniterator", lambda: [x for x in LocalClass()], TypeError,
       "iter() returned non-iterator of type 'int'")
expect("operand", lambda: LocalClass() + 1, TypeError,
       "unsupported operand type(s) for +: 'LocalClass' and 'int'")
expect("concat", lambda: "a" + LocalClass(), TypeError,
       'can only concatenate str (not "LocalClass") to str')
expect("contains", lambda: 1 in LocalClass(), TypeError,
       "argument of type 'LocalClass' is not a container or iterable")
expect("format", lambda: format(LocalClass(), "d"), TypeError,
       "unsupported format string passed to LocalClass.__format__")
expect("int", lambda: int(LocalClass()), TypeError,
       "int() argument must be a string, a bytes-like object or a real number, "
       "not 'LocalClass'")
expect("type.attr", lambda: getattr(LocalClass, "nope"), AttributeError,
       "type object 'LocalClass' has no attribute 'nope'")
expect("type.attr.crossmodule", lambda: getattr(helper.make(), "nope"), AttributeError,
       "type object 'LocalClass' has no attribute 'nope'")
# a declared type keeps the name it was registered with
expect("builtin.len", lambda: len(1), TypeError,
       "object of type 'int' has no len()")

# --- %T: the dict-key/set-element wording keeps module.qualname, dropping
# "builtins" and "__main__"
expect("dict.key.local", lambda: {LocalClass(): 1}, TypeError,
       "cannot use 'factory.<locals>.LocalClass' as a dict key "
       "(unhashable type: 'LocalClass')")
expect("dict.key.moduleclass", lambda: {helper.ModuleClass(): 1}, TypeError,
       "cannot use 'test_type_name_render_helper.ModuleClass' as a dict key "
       "(unhashable type: 'ModuleClass')")
expect("dict.key.modulelocal", lambda: {helper.make()(): 1}, TypeError,
       "cannot use 'test_type_name_render_helper.make.<locals>.LocalClass' "
       "as a dict key (unhashable type: 'LocalClass')")
expect("set.elem.moduleclass", lambda: {helper.ModuleClass()}, TypeError,
       "cannot use 'test_type_name_render_helper.ModuleClass' as a set element "
       "(unhashable type: 'ModuleClass')")

# --- the property wording keeps the qualname (PyType_GetQualName)
expect("property.setter", lambda: setattr(helper.ModuleProp(), "p", 1), AttributeError,
       "property 'p' of 'ModuleProp' object has no setter")
expect("property.deleter", lambda: delattr(helper.ModuleProp(), "p"), AttributeError,
       "property 'p' of 'ModuleProp' object has no deleter")


def prop_factory():
    class LocalProp:
        @property
        def p(self):
            return 1

    return LocalProp


expect("property.qualname", lambda: setattr(prop_factory()(), "p", 1), AttributeError,
       "property 'p' of 'prop_factory.<locals>.LocalProp' object has no setter")

# --- exception repr names the type bare (_PyType_Name)
assert repr(helper.ModuleErr("m")) == "ModuleErr('m')", repr(helper.ModuleErr("m"))
LocalError = type("LocalError", (Exception,), {})
assert repr(LocalError("m")) == "LocalError('m')", repr(LocalError("m"))

# --- bytearray repr names the type bare too (Objects/bytearrayobject.c
# bytearray_repr_lock_held reads _PyType_Name), so a subclass instance is told
# apart from bytearray and eval(repr(x)) round trips to the subclass. bytes is
# the deliberate exception: bytes_repr ignores the type, so a bytes subclass
# reprs as b'x'.
class ModuleBA(bytearray):
    pass


class ModuleBANested:
    class NestedBA(bytearray):
        pass


def ba_factory():
    class LocalBA(bytearray):
        pass

    return LocalBA


assert repr(ModuleBA(b"x")) == "ModuleBA(b'x')", repr(ModuleBA(b"x"))
assert repr(ModuleBA()) == "ModuleBA(b'')", repr(ModuleBA())
assert repr(ba_factory()(b"x")) == "LocalBA(b'x')", repr(ba_factory()(b"x"))
assert repr(ModuleBANested.NestedBA(b"y")) == "NestedBA(b'y')"
assert str(ModuleBA(b"x")) == "ModuleBA(b'x')", str(ModuleBA(b"x"))
assert f"{ModuleBA(b'x')}" == "ModuleBA(b'x')", f"{ModuleBA(b'x')}"
assert repr(bytearray(b"x")) == "bytearray(b'x')", repr(bytearray(b"x"))
RenamedBA = type("RenamedBA", (bytearray,), {})
RenamedBA.__name__ = "Z"
assert repr(RenamedBA(b"x")) == "Z(b'x')", repr(RenamedBA(b"x"))

print("All type-name render tests passed!")

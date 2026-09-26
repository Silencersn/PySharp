"""
qualified names and creation metadata.

Class __qualname__ carries the lexical scope path (PEP 3155): nested
classes join the enclosing class names, function-local classes interleave
'<locals>' segments. The class body preamble seeds __module__/__qualname__
/__firstlineno__ (3.14), so class-body locals() lists them, __firstlineno__
points at the class statement's first line (the first decorator line when
decorated), and the class namespace keeps __module__/__firstlineno__ while
__qualname__ moves to the type accessor. Functions, lambdas, generators,
and coroutines expose __qualname__/__module__ with CPython's read/write
behavior.

CPython 3.14 reference (Python/codegen.c codegen_class_body,
Objects/typeobject.c type_new_set_ht_name, Objects/funcobject.c,
Objects/genobject.c).

:kind: test
"""

# nested classes join the lexical path
class Outer:
    class Inner:
        pass

assert Outer.Inner.__qualname__ == "Outer.Inner"
assert Outer.Inner.__name__ == "Inner"
assert (Outer.Inner.__name__, Outer.Inner.__qualname__) == ("Inner", "Outer.Inner")

class A:
    class B:
        class C:
            pass
assert A.B.C.__qualname__ == "A.B.C"

# function-local classes interleave <locals>
def factory():
    class Local:
        pass
    return Local
assert factory().__qualname__ == "factory.<locals>.Local"

# a global-declared class keeps the bare name
def global_class_maker():
    global GClass
    class GClass:
        pass
global_class_maker()
assert GClass.__qualname__ == "GClass"

# deep lexical nesting
def outer():
    class Inner:
        def m(self):
            class Deep:
                pass
            return Deep
    return Inner
assert outer().m(0).__qualname__ == "outer.<locals>.Inner.m.<locals>.Deep"

# functions, lambdas, generators, coroutines
def top_fn():
    pass
assert top_fn.__qualname__ == "top_fn"
assert top_fn.__module__ == "__main__"

def outer_fn():
    def inner_fn():
        pass
    return inner_fn
assert outer_fn().__qualname__ == "outer_fn.<locals>.inner_fn"

class Normal:
    def method(self):
        pass
assert Normal.method.__qualname__ == "Normal.method"

assert (lambda: 1).__qualname__ == "<lambda>"
def lam_holder():
    return lambda: 1
assert lam_holder().__qualname__ == "lam_holder.<locals>.<lambda>"

def gen_holder():
    yield 1
g = gen_holder()
assert g.__name__ == "gen_holder"
assert g.__qualname__ == "gen_holder"
g.__name__ = "renamed"
g.__qualname__ = "renamed.q"
assert (g.__name__, g.__qualname__) == ("renamed", "renamed.q")
for bad in (lambda: setattr(g, "__name__", 42), lambda: delattr(g, "__name__")):
    try:
        bad()
        assert False, "TypeError expected"
    except TypeError as e:
        assert str(e) == "__name__ must be set to a string object"

async def afn():
    pass
coro = afn()
assert coro.__qualname__ == "afn"
coro.close()

# writability: the decorator metadata idiom
def wrap(fn):
    def wrapper(*a, **k):
        return fn(*a, **k)
    wrapper.__qualname__ = fn.__qualname__
    wrapper.__module__ = fn.__module__
    return wrapper

@wrap
def decorated():
    pass
assert (decorated.__qualname__, decorated.__module__) == ("decorated", "__main__")

# __qualname__ accepts str only, deletion is rejected
try:
    top_fn.__qualname__ = 42
    assert False, "TypeError expected"
except TypeError as e:
    assert str(e) == "__qualname__ must be set to a string object"
try:
    del top_fn.__qualname__
    assert False, "TypeError expected"
except TypeError as e:
    assert str(e) == "__qualname__ must be set to a string object"
assert top_fn.__qualname__ == "top_fn"

# __module__ is a plain member: any value, deletion reads back as None
top_fn.__module__ = "custom.mod"
assert top_fn.__module__ == "custom.mod"
top_fn.__module__ = 42
assert top_fn.__module__ == 42
del top_fn.__module__
assert top_fn.__module__ is None
top_fn.__module__ = "__main__"

# class-body locals() lists the implicit metadata keys
class KBody:
    cv = 1
    cw = 2
    body_locals = sorted(locals().keys())
assert KBody.body_locals == ["__firstlineno__", "__module__", "__qualname__", "cv", "cw"]

# the body reads its own metadata
class KRead:
    q = __qualname__
    m = __module__
assert (KRead.q, KRead.m) == ("KRead", "__main__")

# namespace contents: __qualname__ moves to the accessor, the others stay
assert "__qualname__" not in vars(KBody)
assert "__module__" in vars(KBody)
assert "__firstlineno__" in vars(KBody)
assert vars(KBody)["__module__"] == "__main__"

# an explicit __qualname__ override keeps __name__ untouched
class Q:
    __qualname__ = "custom"
assert (Q.__name__, Q.__qualname__) == ("Q", "custom")
assert "__qualname__" not in vars(Q)

# a non-str override is rejected at class creation
try:
    exec("class QB:\n    __qualname__ = 42\n", globals())
    assert False, "TypeError expected"
except TypeError as e:
    assert str(e) == "type __qualname__ must be a str, not int"

# firstlineno: class-statement line; the first decorator line when decorated
class Plain:
    pass
assert Plain.__firstlineno__ == 172

def noopdec(c):
    return c

@noopdec
class Deco:
    pass
assert Deco.__firstlineno__ == 179

# firstlineno is a plain class attribute
Plain.__firstlineno__ = 5
assert Plain.__firstlineno__ == 5
del Plain.__firstlineno__
assert "__firstlineno__" not in vars(Plain)

# exec namespace: __module__ follows the namespace's __name__
ns = {"__name__": "custom_module"}
exec("class E1: pass", ns)
assert (ns["E1"].__qualname__, ns["E1"].__module__) == ("E1", "custom_module")

# dynamic type() metadata
X = type("X", (), {})
assert (X.__qualname__, X.__module__) == ("X", "__main__")
assert type("M", (), {"__module__": "mm"}).__module__ == "mm"
Y = type("Y", (), {"__qualname__": "qq"})
assert (Y.__name__, Y.__qualname__) == ("Y", "qq")

print("test_qualname_metadata passed")

"""
Regression: __class__ is writable (CPython object_set_class). Only the type
pointer moves, so type()/isinstance()/method lookup follow the new class at
once, __init__ does not run again and the instance dict stays in place. A
non-class value, an immutable type, or a layout mismatch raises TypeError
with the CPython wording, and an unshadowed __class__ is never deletable.
Exercises PyTypeObject.DefaultSetAttr and DefaultDelAttr.
"""

import sys


def expect_error(label, exc_type, expected, fn):
    try:
        fn()
    except exc_type as error:
        assert str(error) == expected, f'{label}: {str(error)!r}'
        return
    except Exception as error:
        raise AssertionError(f'{label}: {type(error).__name__}: {error}')

    raise AssertionError(f'{label}: no {exc_type.__name__}')


MUTABLE_ONLY = '__class__ assignment only supported for mutable types or ModuleType subclasses'


def assign_class(target, value):
    target.__class__ = value


def delete_class(target):
    del target.__class__


class Plain:
    def who(self):
        return 'Plain'


class Other:
    def who(self):
        return 'Other'


class Parent:
    def who(self):
        return 'Parent'


class Child(Parent):
    def who(self):
        return 'Child'


class Map1(dict):
    pass


class Map2(Map1):
    pass


class Int1(int):
    pass


class Int2(Int1):
    pass


class Float1(float):
    pass


class Float2(Float1):
    pass


class Text1(str):
    pass


class Text2(Text1):
    pass


class Seq1(list):
    pass


class Seq2(Seq1):
    pass


class Err1(Exception):
    pass


class Err2(Err1):
    pass


class ModuleSub(type(sys)):
    pass


class Meta(type):
    pass


class WithMeta(metaclass=Meta):
    pass


class First(metaclass=Meta):
    pass


class Second(metaclass=Meta):
    pass


# --- a compatible swap takes effect at once ---
inst = Plain()
assert inst.__class__ is Plain
inst.__class__ = Other
assert type(inst) is Other
assert inst.__class__ is Other
assert inst.who() == 'Other'
assert isinstance(inst, Other)
assert not isinstance(inst, Plain)
assert assign_class(inst, Plain) is None
assert type(inst) is Plain and inst.who() == 'Plain'

# --- the instance dict stays in place and nothing leaks into it ---
carrier = Plain()
carrier.x = 1
storage = carrier.__dict__
carrier.__class__ = Other
assert carrier.x == 1
assert carrier.__dict__ is storage
assert sorted(vars(carrier)) == ['x']

# --- no __init__ on the new class, no __setattr__ interception ---
calls = []


class Tracked:
    def __init__(self, *args):
        calls.append('init')

    def __setattr__(self, name, value):
        calls.append(('set', name))
        object.__setattr__(self, name, value)


tracked = Plain()
tracked.__class__ = Tracked
assert calls == []
assert type(tracked) is Tracked
assert setattr(tracked, 'tag', 1) is None
assert calls == [('set', 'tag')]
assert tracked.tag == 1

# --- both directions inside a hierarchy ---
child = Child()
child.__class__ = Parent
assert type(child) is Parent and child.who() == 'Parent'
assert isinstance(child, Parent) and not isinstance(child, Child)
parent = Parent()
parent.__class__ = Child
assert type(parent) is Child and parent.who() == 'Child'
assert isinstance(parent, Child)
assert super(Child, parent).who() == 'Parent'

# --- builtin-layout subclasses swap and keep their payload ---
mapping = Map1(a=1)
mapping.__class__ = Map2
assert type(mapping) is Map2
assert dict(mapping) == {'a': 1}
assert isinstance(mapping, Map2)
number = Int1(7)
number.tag = 'kept'
number.__class__ = Int2
assert type(number) is Int2
assert int(number) == 7 and number.tag == 'kept'
assert isinstance(number, Int2)
ratio = Float1(1.5)
ratio.__class__ = Float2
assert type(ratio) is Float2 and float(ratio) == 1.5
text = Text1('hi')
text.__class__ = Text2
assert type(text) is Text2 and str(text) == 'hi'
sequence = Seq1([1])
sequence.tag = 'kept'
sequence.__class__ = Seq2
assert type(sequence) is Seq2
assert list(sequence) == [1] and sequence.tag == 'kept'
error = Err1('m', 1)
error.__class__ = Err2
assert type(error) is Err2
assert error.args == ('m', 1) and str(error) == "('m', 1)"

# --- instances of a class with a metaclass swap against plain classes ---
meta_inst = WithMeta()
assert type(meta_inst) is WithMeta
meta_inst.__class__ = Plain
assert type(meta_inst) is Plain
meta_inst.__class__ = WithMeta
assert type(meta_inst) is WithMeta

# --- a module swaps against a ModuleType subclass and back ---
module_type = type(sys)
sys.__class__ = ModuleSub
assert type(sys) is ModuleSub
assert isinstance(sys, ModuleSub)
sys.__class__ = module_type
assert type(sys) is module_type

# --- dynamically created classes ---
Dynamic1 = type('Dynamic1', (), {})
Dynamic2 = type('Dynamic2', (), {'who': lambda self: 'Dynamic2'})
dynamic = Dynamic1()
dynamic.__class__ = Dynamic2
assert type(dynamic) is Dynamic2
assert dynamic.who() == 'Dynamic2'

# --- a non-class value is rejected with the value's own type name ---
expect_error('list value', TypeError, "__class__ must be set to a class, not 'list' object",
             lambda: assign_class(Plain(), []))
expect_error('str value', TypeError, "__class__ must be set to a class, not 'str' object",
             lambda: assign_class(Plain(), 'x'))
expect_error('int value', TypeError, "__class__ must be set to a class, not 'int' object",
             lambda: assign_class(Plain(), 5))
expect_error('none value', TypeError, "__class__ must be set to a class, not 'NoneType' object",
             lambda: assign_class(Plain(), None))
expect_error('instance value', TypeError, "__class__ must be set to a class, not 'Plain' object",
             lambda: assign_class(Plain(), Plain()))


class Holder:
    class Inner:
        pass


expect_error('nested value', TypeError, "__class__ must be set to a class, not 'Inner' object",
             lambda: assign_class(Plain(), Holder.Inner()))

# --- immutable instances and static types are refused ---
expect_error('to int', TypeError, MUTABLE_ONLY, lambda: assign_class(Plain(), int))
expect_error('to str', TypeError, MUTABLE_ONLY, lambda: assign_class(Plain(), str))
expect_error('to type', TypeError, MUTABLE_ONLY, lambda: assign_class(Plain(), type))
expect_error('from int', TypeError, MUTABLE_ONLY, lambda: assign_class(5, Plain))
expect_error('from bool', TypeError, MUTABLE_ONLY, lambda: assign_class(True, Plain))
expect_error('from list', TypeError, MUTABLE_ONLY, lambda: assign_class([1], Plain))
expect_error('from dict', TypeError, MUTABLE_ONLY, lambda: assign_class({}, Plain))
expect_error('from tuple', TypeError, MUTABLE_ONLY, lambda: assign_class((1,), Plain))
expect_error('from function', TypeError, MUTABLE_ONLY, lambda: assign_class(assign_class, Plain))
expect_error('from static exception', TypeError, MUTABLE_ONLY, lambda: assign_class(Exception('m'), Err1))
expect_error('list subclass to list', TypeError, MUTABLE_ONLY, lambda: assign_class(Seq1([1]), list))
expect_error('module to int', TypeError, MUTABLE_ONLY, lambda: assign_class(sys, int))
expect_error('heap class to class', TypeError, MUTABLE_ONLY, lambda: assign_class(Plain, Other))

# --- static type objects keep their own message ---
expect_error('static type object', TypeError, "cannot set '__class__' attribute of immutable type 'int'",
             lambda: assign_class(int, Plain))

# --- layouts must match, and the message names the new class first ---
expect_error('user to list subclass', TypeError, "__class__ assignment: 'Seq1' object layout differs from 'Plain'",
             lambda: assign_class(Plain(), Seq1))
expect_error('user to int subclass', TypeError, "__class__ assignment: 'Int1' object layout differs from 'Plain'",
             lambda: assign_class(Plain(), Int1))
expect_error('user to module subclass', TypeError, "__class__ assignment: 'ModuleSub' object layout differs from 'Plain'",
             lambda: assign_class(Plain(), ModuleSub))
expect_error('metaclass swap', TypeError, "__class__ assignment: 'Second' object layout differs from 'Meta'",
             lambda: assign_class(First, Second))

# --- an unshadowed __class__ is not deletable ---
expect_error('delete instance', TypeError, "can't delete __class__ attribute", lambda: delete_class(Plain()))
expect_error('delete frozen', TypeError, "can't delete __class__ attribute", lambda: delete_class(5))
expect_error('delete class', TypeError, "can't delete __class__ attribute", lambda: delete_class(Plain))
expect_error('delete module', TypeError, "can't delete __class__ attribute", lambda: delattr(sys, '__class__'))

# --- rejected writes leave no trace ---
clean = Plain()
expect_error('rejected write', TypeError, MUTABLE_ONLY, lambda: assign_class(clean, int))
assert vars(clean) == {}
assert type(clean) is Plain
assert '__class__' not in Plain.__dict__

# --- a class body shadowing __class__ keeps plain attribute semantics ---
class Shadow:
    __class__ = 5


shadow = Shadow()
shadow.__class__ = Other
assert type(shadow) is Shadow
assert shadow.__dict__['__class__'] is Other
expect_error('shadow delete', AttributeError, "'Shadow' object has no attribute '__class__'",
             lambda: delete_class(Shadow()))


class Guarded:
    @property
    def __class__(self):
        return 'shadow'


expect_error('property write', AttributeError,
             "property '__class__' of 'Guarded' object has no setter",
             lambda: assign_class(Guarded(), Other))
expect_error('property delete', AttributeError,
             "property '__class__' of 'Guarded' object has no deleter",
             lambda: delete_class(Guarded()))

# --- builtin call forms reach the same guard ---
expect_error('setattr form', TypeError, MUTABLE_ONLY,
             lambda: setattr(Plain(), '__class__', int))
expect_error('delattr form', TypeError, "can't delete __class__ attribute",
             lambda: delattr(Plain(), '__class__'))
assert hasattr(Plain(), '__class__')

print("test_class_reassign_regression passed")

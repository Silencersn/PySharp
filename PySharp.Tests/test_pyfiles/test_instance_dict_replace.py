"""
an instance __dict__ is writable (CPython subtype_setdict ->
_PyObject_SetDict). The assigned dict replaces the instance storage
wholesale, a non-dict raises TypeError, a dict subclass is accepted and
keeps its identity, and deletion drops the dict so the next read
materializes an empty one.
Exercises PyTypeObject.DefaultSetAttr, DefaultDelAttr and PyCore.

:kind: test
"""

import math


def expect_error(label, exc_type, expected, fn):
    try:
        fn()
    except exc_type as error:
        assert str(error) == expected, f'{label}: {str(error)!r}'
        return
    except Exception as error:
        raise AssertionError(f'{label}: {type(error).__name__}: {error}')

    raise AssertionError(f'{label}: no {exc_type.__name__}')


def assign_dict(target, value):
    target.__dict__ = value


def delete_dict(target):
    del target.__dict__


NO_DICT = " and no __dict__ for setting new attributes"


class Plain:
    pass


# --- the replacement reaches the next read and drops the old attributes ---
inst = Plain()
inst.old = 1
assigned = {'z': 3}
assign_dict(inst, assigned)
assert inst.z == 3
assert not hasattr(inst, 'old')
assert sorted(inst.__dict__) == ['z']
assert inst.__dict__ is assigned
assert vars(inst) is inst.__dict__

# --- the assigned dict stays live in both directions ---
live = {'b': 2}
other = Plain()
other.a = 1
assign_dict(other, live)
assert other.__dict__ is live
assert other.b == 2
assert not hasattr(other, 'a')
live['c'] = 3
assert other.c == 3
other.e = 4
assert live['e'] == 4

# --- two instances can share one dict ---
first = Plain()
second = Plain()
first.x = 1
second.y = 2
assign_dict(first, second.__dict__)
assert first.y == 2
assert not hasattr(second, 'x')
first.z = 5
assert second.z == 5

# --- a non-dict is rejected with the CPython message ---
expect_error('list', TypeError, "__dict__ must be set to a dictionary, not a 'list'",
             lambda: assign_dict(Plain(), [1]))
expect_error('str', TypeError, "__dict__ must be set to a dictionary, not a 'str'",
             lambda: assign_dict(Plain(), 'ab'))
expect_error('none', TypeError, "__dict__ must be set to a dictionary, not a 'NoneType'",
             lambda: assign_dict(Plain(), None))
expect_error('int', TypeError, "__dict__ must be set to a dictionary, not a 'int'",
             lambda: assign_dict(Plain(), 5))
expect_error('instance', TypeError, "__dict__ must be set to a dictionary, not a 'Plain'",
             lambda: assign_dict(Plain(), Plain()))

# a rejected assignment leaves the instance dict alone
kept = Plain()
kept.k = 1
expect_error('kept', TypeError, "__dict__ must be set to a dictionary, not a 'list'",
             lambda: assign_dict(kept, [1]))
assert sorted(kept.__dict__) == ['k']


# the reported type name is the bare name, not the dotted qualname
class Outer:
    class Inner:
        pass


expect_error('nested', TypeError, "__dict__ must be set to a dictionary, not a 'Inner'",
             lambda: assign_dict(Outer.Inner(), Outer.Inner()))


# --- a dict subclass is accepted and keeps its identity ---
class MyDict(dict):
    pass


sub = Plain()
sub.q = 1
custom = MyDict(k=9)
assign_dict(sub, custom)
assert sub.__dict__ is custom
assert type(sub.__dict__) is MyDict
assert sub.k == 9
assert not hasattr(sub, 'q')

# --- deletion drops the dict; the next read materializes an empty one ---
dropped = Plain()
dropped.r = 1
old = dropped.__dict__
delete_dict(dropped)
assert sorted(dropped.__dict__) == []
assert dropped.__dict__ is not old
assert old == {'r': 1}
assert not hasattr(dropped, 'r')
delete_dict(dropped)
assert sorted(dropped.__dict__) == []


# --- functions, exceptions and builtin-layout subclasses share the path ---
def func():
    return 1


func.x = 7
assign_dict(func, {'a': 1})
assert func.a == 1
assert not hasattr(func, 'x')
expect_error('func', TypeError, "__dict__ must be set to a dictionary, not a 'list'",
             lambda: assign_dict(func, [1]))

error = ValueError('boom')
error.extra = 1
assign_dict(error, {'n': 3})
assert error.n == 3
assert not hasattr(error, 'extra')


class ListSub(list):
    pass


seq = ListSub()
seq.a = 1
assign_dict(seq, {'m': 2})
assert seq.m == 2
assert not hasattr(seq, 'a')
assert sorted(seq.__dict__) == ['m']


# --- a type object reads __dict__ through a getset with no setter ---
class TypeHolder:
    def m(self):
        return 1


expect_error('type', AttributeError, "attribute '__dict__' of 'type' objects is not writable",
             lambda: assign_dict(TypeHolder, {'m2': 1}))
expect_error('type del', AttributeError, "attribute '__dict__' of 'type' objects is not writable",
             lambda: delete_dict(TypeHolder))
assert TypeHolder().m() == 1

# --- frozen and dict-less objects keep their shapes ---
expect_error('str frozen', AttributeError, "'str' object has no attribute '__dict__'" + NO_DICT,
             lambda: assign_dict('x', {}))
expect_error('int frozen', AttributeError, "'int' object has no attribute '__dict__'" + NO_DICT,
             lambda: assign_dict(5, {}))
expect_error('int read', AttributeError, "'int' object has no attribute '__dict__'",
             lambda: (5).__dict__)
expect_error('str type', TypeError, "cannot set '__dict__' attribute of immutable type 'str'",
             lambda: assign_dict(str, {}))
expect_error('object type', TypeError, "cannot set '__dict__' attribute of immutable type 'object'",
             lambda: assign_dict(object, {}))
expect_error('type type', TypeError, "cannot set '__dict__' attribute of immutable type 'type'",
             lambda: assign_dict(type, {}))
expect_error('module', AttributeError, 'readonly attribute',
             lambda: assign_dict(math, {}))


# --- a method has no __dict__, and a call-position read resolves the same ---
class MethodHolder:
    def m(self):
        pass


expect_error('method', AttributeError,
             "'method' object has no attribute '__dict__'" + NO_DICT,
             lambda: assign_dict(MethodHolder().m, {}))
expect_error('generator', AttributeError, "'generator' object has no attribute '__dict__'",
             lambda: (item for item in []).__dict__)

holder = MethodHolder()
holder.k = 1
expect_error('call', TypeError, "'dict' object is not callable", lambda: holder.__dict__())
assert sorted(holder.__dict__.items()) == [('k', 1)]

# --- writing through the dict still works ---
through = Plain()
through.__dict__['k'] = 1
assert through.k == 1

print("test_instance_dict_replace passed")

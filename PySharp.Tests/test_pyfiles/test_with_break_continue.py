"""Verifies that break, continue, and return crossing a with block run __exit__ cleanup before jumping, keeping the operand stack and handler records intact.

Covers nested with, multiple with items, return values, for-else, class-body loops, generators, stale-record exception dispatch, and __exit__ raising during unwinding.

:kind: test
"""

# Regression test: break/continue/return crossing with blocks.
#
# A with body keeps [exit, manager] resident on the operand stack under a
# runtime handler record. break/continue/return used to emit bare jumps, so
# __exit__ never ran, the pair leaked (stack assert / iterator corruption /
# stack overflow), and the dead handler record misdispatched later
# exceptions. The compiler now tracks regions and inlines the __exit__
# cleanup plus record disposal before every such jump, like CPython's
# codegen_unwind_fblock_stack.
#
# CPython 3.14 references: break/continue crossing WITH run __exit__(None,
# None, None) first; return keeps its value below the cleanup
# (preserve_tos); an exception raised by the unwinding __exit__ dispatches
# to outer handlers only (POP_BLOCK precedes the call).

class C:
    def __init__(self, tag=''):
        self.tag = tag
    def __enter__(self):
        print('enter', self.tag)
        return 1
    def __exit__(self, *e):
        print('exit', self.tag)
        return False

# 1. break in a for loop: __exit__ runs, iterator slot stays balanced
for i in range(2):
    with C('b'):
        break
print('done-break')

# 2. continue in a for loop: every iteration exits the context first
for i in range(2):
    with C('c'):
        continue
print('done-continue')

# 3. while variant (leaked values used to accumulate silently)
n = 0
while n < 3:
    n += 1
    with C('w'):
        if n == 2:
            break
print('done-while', n)

# 4. nested with statements: both exits run innermost-first
for i in range(2):
    with C('out'):
        with C('in'):
            break
print('done-nested')

# 5. two with items in one statement
for i in range(2):
    with C('x1'), C('x2'):
        break
print('done-items')

# 6. break out of the inner of two for loops must not corrupt the outer
for i in range(2):
    for j in range(2):
        with C():
            break
    else:
        print('else', i)
print('done-twofors')

# 7. return from with inside for: previously called a stale iterator slot
def f7():
    for i in range(3):
        with C('r'):
            return i
assert f7() == 0

def f7b():
    n = 0
    while n < 3:
        n += 1
        with C('rb'):
            return n * 10
assert f7b() == 10

# 8. the with-bound value is evaluated before any cleanup runs
def f8():
    for i in range(2):
        with C() as x:
            return x + i
assert f8() == 1

# 9. a dead record must not intercept later exceptions
class Swallow:
    def __enter__(self):
        return 1
    def __exit__(self, *e):
        print('swallow-exit called with', e[0] is not None)
        return True  # would eat any exception it wrongly sees
n = 0
while n < 2:
    n += 1
    with Swallow():
        if n == 1:
            break
try:
    raise ValueError('boom')
except ValueError as e:
    assert e.args[0] == 'boom'
print('no-stale-dispatch')

# 10. an exception raised by the unwinding __exit__ escapes outward
class Bad:
    def __enter__(self):
        return 1
    def __exit__(self, *e):
        raise RuntimeError('exit-boom')
try:
    for i in range(3):
        with Bad():
            break
except RuntimeError as e:
    assert e.args[0] == 'exit-boom'
print('exit-raise-ok')

# 11. for-else: break still skips the else clause
for i in range(5):
    with C():
        if i == 2:
            break
else:
    assert False, 'else must not run'
print('forelse-ok')

# 12. class-body loops and with blocks unwind within the class code object
class K:
    found = None
    for i in range(3):
        with C('cls'):
            if i == 1:
                found = i
                break
assert K.found == 1

# 13. generators: break/return inside with inside a generator frame
def g13():
    for i in range(3):
        with C('gen'):
            yield i
            if i == 1:
                break
    yield 'end'
assert list(g13()) == [0, 1, 'end']

def g13b():
    for i in range(3):
        with C('genr'):
            yield i
            if i == 0:
                return
assert list(g13b()) == [0]

print("test_with_break_continue passed")

"""Verifies that break/continue/return escaping a try body run the finally body exactly once, that the finally body's own control flow overrides the pending exit, and that `except ... as name` bindings are deleted on every handler exit.

:kind: test
"""

# Regression test: break/continue/return crossing try/finally and except
# handlers.
#
# try bodies run under a runtime handler record; break/continue/return used
# to jump straight out, silently skipping finally bodies and leaking the
# `except ... as name` binding. The compiler now copies the finally body
# inline into every such jump (dropping the record first), clears the
# in-flight exception when leaving a handler body, and deletes the handler
# name — CPython's FINALLY_TRY / HANDLER_CLEANUP unwinding.
#
# CPython 3.14 references: a control-flow jump out of try runs the finally
# body exactly once; a return/break inside that finally body overrides the
# pending one; `except E as e` deletes e on every handler exit.

class MyErr(Exception):
    pass

# 1. break/continue run the finally body on the escaping iteration too
for i in range(3):
    try:
        if i == 1:
            break
    finally:
        print('fin-break', i)
print('done-break')

seen = []
for i in range(3):
    try:
        if i == 1:
            continue
    finally:
        seen.append(i)
assert seen == [0, 1, 2]

# 2. return unwinds through the finally body, value preserved below it
def f2():
    for i in range(3):
        try:
            if i == 1:
                return i + 10
        finally:
            print('fin-ret', i)
    return -1
assert f2() == 11

# 3. the finally body's own control flow wins
def f3a():
    try:
        return 1
    finally:
        return 2
assert f3a() == 2

def f3b():
    for i in range(3):
        try:
            if i == 1:
                break
        finally:
            return 'fin-wins'
    return 'after'
assert f3b() == 'fin-wins'

for i in range(3):
    try:
        if i == 1:
            break
    finally:
        if i == 1:
            print('fin sees', i)
            break
print('done-inner-break')

# 4. except handler bodies: break/return clear the in-flight exception and
#    run the finally body of the same statement
log = []
for i in range(4):
    try:
        raise MyErr(i)
    except MyErr as e:
        if i == 2:
            break
        log.append(e.args[0])
    finally:
        log.append('fin%d' % i)
assert log == [0, 'fin0', 1, 'fin1', 'fin2']

def f4():
    for i in range(3):
        try:
            raise MyErr(i)
        except MyErr as e:
            return e.args[0] * 100
    return -1
assert f4() == 0

# 5. the handler binding is deleted on every exit path (also after `del e`)
for i in range(3):
    try:
        raise MyErr(i)
    except MyErr as e:
        if i == 1:
            break
print('done-as-break')
try:
    print(e)
    assert False, 'e must be deleted'
except NameError:
    pass

def f5():
    try:
        raise MyErr(7)
    except MyErr as e:
        return e.args[0]
try:
    f5()
    print(e)
    assert False, 'e must be deleted'
except NameError:
    pass

for i in range(2):
    try:
        raise MyErr(i)
    except MyErr as e:
        del e
        break
print('del-then-break-ok')

# 6. stacked regions: with under try under a loop, all unwound in order
class C:
    def __init__(self, tag):
        self.tag = tag
    def __enter__(self):
        print('enter', self.tag)
        return 1
    def __exit__(self, *e):
        print('exit', self.tag)
        return False

def f6():
    for i in range(3):
        with C('w1'):
            try:
                with C('w2'):
                    if i == 1:
                        return i
            finally:
                print('fin-mixed', i)
    return -1
assert f6() == 1

# 7. an exception raised inside the inline finally copy propagates outward
try:
    for i in range(3):
        try:
            if i == 0:
                break
        finally:
            raise MyErr('from-finally')
except MyErr as e:
    assert e.args[0] == 'from-finally'
print('finally-raise-ok')

# 8. loops inside the finally body keep their own break scoping
for i in range(2):
    try:
        break
    finally:
        for j in range(3):
            if j == 1:
                break
        print('inner loop ended at', j)
print('done-inner-loop')

print("test_finally_control_flow passed")

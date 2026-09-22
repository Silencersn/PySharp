# Unwind emission regression, aligned with CPython:
#
# a finally body copied inline for an in-flight exit (return/break/
# continue) must compile with the regions lexically outside the try
# visible, so break/continue/return inside the copy resolve against the
# enclosing constructs; the pending exit is discarded (the carried value
# is popped) and the copy's own jump wins.
# (PEP 765 SyntaxWarnings for these constructs are expected on stderr.)

# return in try + break in finally: the return is discarded
def f1():
    while True:
        try:
            return 1
        finally:
            break
    return 2
assert f1() == 2

# same through a for loop
def f2():
    for i in range(10):
        try:
            return i
        finally:
            break
    return 'end'
assert f2() == 'end'

# continue in finally cancels the return and the loop keeps going
def f3():
    total = 0
    for i in range(4):
        try:
            if i == 2:
                return -1
            total += i
        finally:
            if i == 2:
                continue
    return total
assert f3() == 4

# nested try: the inner finally's break discards the return but the
# outer finally still runs on the way out
def f4():
    log = []
    while True:
        try:
            try:
                return 1
            finally:
                log.append('inner')
                break
        finally:
            log.append('outer')
    return log
assert f4() == ['inner', 'outer']

# a with between the loop and the try calls __exit__ on the break path
def f5():
    log = []
    class CM:
        def __enter__(self):
            return self
        def __exit__(self, *a):
            log.append('exit')
            return False
    while True:
        with CM():
            try:
                return 1
            finally:
                break
    return log
assert f5() == ['exit']

# return inside an except handler, cancelled by the finally's break
def f6():
    while True:
        try:
            raise ValueError('x')
        except ValueError:
            return 1
        finally:
            break
    return 'end'
assert f6() == 'end'

# a return inside the finally wins over the in-flight return; outer
# cleanups still run
def f7():
    log = []
    while True:
        try:
            try:
                return 1
            finally:
                log.append('f')
                return 2
        finally:
            log.append('outer')
    return log
assert f7() == 2

# exception path: runtime unwinding through a break in finally
def f8():
    while True:
        try:
            raise ValueError('x')
        except ValueError:
            pass
        finally:
            break
    return 'ok'
assert f8() == 'ok'

# break in the try body + break in the finally (double break)
def f9():
    while True:
        try:
            break
        finally:
            break
    return 'done'
assert f9() == 'done'

print("test_unwind_finally_jumps_regression passed")

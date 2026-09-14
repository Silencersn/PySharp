# Regression: the implicit cleanup of an except-as name is `name = None;
# del name` and runs on every handler exit. A plain `del` made the cleanup
# raise an uncatchable NameError after an explicit `del name` in the body,
# and an escaping body skipped the cleanup entirely, leaking the binding.

# original repro: two except-as blocks, the first deletes its name
try:
    raise ValueError('v')
except ValueError as e:
    del e
    try:
        print(e)
    except NameError as ne:
        assert str(ne) == "name 'e' is not defined"

try:
    raise ValueError('w')
except ValueError as e2:
    pass
finally:
    try:
        print(e2)
    except NameError as ne:
        assert str(ne) == "name 'e2' is not defined"

# both handlers delete their names explicitly
try:
    raise ValueError('a')
except ValueError as ea:
    del ea
try:
    raise ValueError('b')
except ValueError as eb:
    del eb
    try:
        print(eb)
    except NameError:
        pass

# control: no explicit del, names still vanish after each handler
try:
    raise ValueError('c')
except ValueError as ec:
    assert str(ec) == 'c'
try:
    print(ec)
except NameError:
    pass

# the body is usable after an explicit del + rebind inside the handler
try:
    raise ValueError('d')
except ValueError as ed:
    del ed
    ed = 1
    assert ed == 1

# an escaping body still cleans the name (no leaked binding)
try:
    try:
        raise ValueError('v')
    except ValueError as ee:
        raise KeyError('k')
except KeyError:
    try:
        print(ee)
    except NameError as ne:
        assert str(ne) == "name 'ee' is not defined"

# except* with an explicit del: the cleanup must not turn into a
# sub-exception escaping as an ExceptionGroup
try:
    try:
        raise ValueError('v')
    except* ValueError as es:
        del es
        try:
            print(es)
        except NameError as ne:
            assert str(ne) == "name 'es' is not defined"
except Exception:
    raise AssertionError('cleanup must not raise')

# except* escape path: body raising still cleans the name
try:
    try:
        raise ValueError('v')
    except* ValueError as es2:
        raise KeyError('k')
except* KeyError:
    try:
        print(es2)
    except NameError as ne:
        assert str(ne) == "name 'es2' is not defined"

# function scope: fast-local name, cleaned after the handler
def f_local():
    try:
        raise ValueError('v')
    except ValueError as fl:
        assert str(fl) == 'v'
    return 'ok'
assert f_local() == 'ok'

# function scope with an explicit del inside the handler
def f_local_del():
    try:
        raise ValueError('v')
    except ValueError as fld:
        del fld
    return 'ok'
assert f_local_del() == 'ok'

# return through the handler empties the captured cell (CPython deletes
# the name even on the return path)
def f_cell():
    try:
        raise ValueError('v')
    except ValueError as fc:
        return lambda: fc
g = f_cell()
try:
    g()
    raise AssertionError('cell should be empty')
except NameError as ne:
    assert 'fc' in str(ne)

# generator: the name stays alive across yields, cleaned after the handler
def gen():
    try:
        raise ValueError('v')
    except ValueError as gy:
        yield ('in-handler', str(gy))
        yield ('after-yield', str(gy))
    yield 'end'
assert list(gen()) == [('in-handler', 'v'), ('after-yield', 'v'), 'end']

# a bare raise in a finally re-raises the escaping exception, not the
# already-handled one
try:
    try:
        raise ValueError('v')
    except ValueError as ebr:
        raise KeyError('k')
    finally:
        raise
except KeyError as k:
    assert k.args == ('k',)

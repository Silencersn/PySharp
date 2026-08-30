import warnings

# warn_explicit deduplicates within the same registry, and separate registries do not.
r = {}
with warnings.catch_warnings(record=True) as records:
    warnings.warn_explicit("boom", UserWarning, "source.py", 7, module="pkg.mod", registry=r, source="obj")
    warnings.warn_explicit("boom", UserWarning, "other.py", 7, module="pkg.mod", registry=r)
assert len(records) == 1, records
assert records[0].filename == "source.py", records[0].filename
assert records[0].lineno == 7, records[0].lineno
assert records[0].source == "obj", records[0].source

r1 = {}
r2 = {}
with warnings.catch_warnings(record=True) as records:
    warnings.warn_explicit("boom", UserWarning, "source.py", 7, registry=r1)
    warnings.warn_explicit("boom", UserWarning, "source.py", 7, registry=r2)
assert len(records) == 2, records

# registry contents are honored in Python dictionaries.
r = {'version': 0, ('boom', UserWarning, 7): True}
with warnings.catch_warnings(record=True) as records:
    warnings.warn_explicit('boom', UserWarning, 'source.py', 7, registry=r)
assert len(records) == 1, records
assert r['version'] > 0, r
assert r[('boom', UserWarning, 7)] is True, r

r.clear()
with warnings.catch_warnings(record=True) as records:
    warnings.warn_explicit('boom', UserWarning, 'source.py', 7, registry=r)
assert len(records) == 1, records
assert r['version'] > 0, r
assert r[('boom', UserWarning, 7)] is True, r

# registry=None disables deduplication.
with warnings.catch_warnings(record=True) as records:
    warnings.warn_explicit('boom', UserWarning, 'source.py', 7, registry=None)
    warnings.warn_explicit('boom', UserWarning, 'source.py', 7, registry=None)
assert len(records) == 2, records

# warning message records report filename, lineno, line, category, etc.
with warnings.catch_warnings(record=True) as records:
    warnings.warn("boom")
item = records[0]
assert item.category is UserWarning, item.category
assert str(item.message) == "boom", item.message
assert item.filename is not None, item.filename
assert isinstance(item.lineno, int) and item.lineno > 0, item.lineno
assert item.file is None, item.file
assert item.line is None or "warnings.warn(" in item.line, item.line

print("test_warnings_module_registry passed")

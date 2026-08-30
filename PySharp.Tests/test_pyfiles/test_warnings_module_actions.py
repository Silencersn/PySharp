import warnings

# always action shows the warning repeatedly at the same site
with warnings.catch_warnings(record=True) as records:
    warnings.simplefilter("always")
    warnings.warn_explicit("boom", UserWarning, "mod.py", 7)
    warnings.warn_explicit("boom", UserWarning, "mod.py", 7)
assert len(records) == 2, records

# module action dedups within the same module, but keeps separate modules apart
mod_reg = {}
mod2_reg = {}
with warnings.catch_warnings(record=True) as records:
    warnings.filterwarnings("module", category=UserWarning)
    warnings.warn_explicit("boom", UserWarning, "mod.py", 7, module="mod.py", registry=mod_reg)
    warnings.warn_explicit("boom", UserWarning, "mod.py", 9, module="mod.py", registry=mod_reg)
    warnings.warn_explicit("boom", UserWarning, "mod2.py", 7, module="mod2.py", registry=mod2_reg)
assert [item.filename for item in records] == ["mod.py", "mod2.py"], records

# once action dedups across different files / lines
with warnings.catch_warnings(record=True) as records:
    warnings.filterwarnings("once", category=UserWarning)
    warnings.warn_explicit("boom", UserWarning, "mod.py", 7)
    warnings.warn_explicit("boom", UserWarning, "mod2.py", 7)
    warnings.warn_explicit("boom", UserWarning, "mod3.py", 9)
assert len(records) == 1, records
assert records[0].filename == "mod.py", records[0].filename

# once action dedups within the same explicit registry
registry = {}
with warnings.catch_warnings(record=True) as records:
    warnings.filterwarnings("once", category=UserWarning)
    warnings.warn_explicit("boom", UserWarning, "mod.py", 7, module="mod.py", registry=registry)
    warnings.warn_explicit("boom", UserWarning, "other.py", 9, module="other.py", registry=registry)
assert len(records) == 1, records
assert records[0].filename == "mod.py", records[0].filename

# resetwarnings() bumps the filter version so a registry forgets earlier warns
registry = {}
with warnings.catch_warnings(record=True) as records:
    warnings.warn_explicit("boom", UserWarning, "mod.py", 7, registry=registry)
    warnings.warn_explicit("boom", UserWarning, "mod.py", 7, registry=registry)
    assert len(records) == 1, records
    warnings.resetwarnings()
    warnings.warn_explicit("boom", UserWarning, "mod.py", 7, registry=registry)
assert len(records) == 2, records

print("test_warnings_module_actions passed")

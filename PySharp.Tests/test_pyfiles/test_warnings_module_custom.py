import warnings

# base Warning category filter matches a subclass category
with warnings.catch_warnings(record=True) as records:
    warnings.filterwarnings("ignore", category=Warning)
    warnings.warn_explicit("boom", UserWarning, "mod.py", 7)
assert len(records) == 0, records

# a user-defined warning derives its own category
class MyWarning(UserWarning):
    pass

with warnings.catch_warnings(record=True) as records:
    warnings.simplefilter("always")
    warnings.warn(MyWarning("boom"))
assert len(records) == 1, records
assert records[0].category is MyWarning, records[0].category
assert str(records[0].message) == "boom", records[0].message

# a filter matching a user-defined warning category suppresses it
with warnings.catch_warnings(record=True) as records:
    warnings.filterwarnings("ignore", category=MyWarning)
    warnings.warn(MyWarning("boom"))
assert len(records) == 0, records

print("test_warnings_module_custom passed")

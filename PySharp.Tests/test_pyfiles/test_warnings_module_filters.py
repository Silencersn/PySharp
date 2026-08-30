import warnings

# simplefilter ignore/error
with warnings.catch_warnings(record=True) as records:
    warnings.simplefilter("ignore")
    warnings.warn("boom")
    warnings.warn("other")
assert len(records) == 0, records

try:
    with warnings.catch_warnings(record=True) as records:
        warnings.simplefilter("error")
        warnings.warn("boom")
    raise AssertionError("warnings.simplefilter('error') should raise")
except UserWarning:
    pass

# catch_warnings should restore the outer state
with warnings.catch_warnings(record=True) as outer:
    warnings.simplefilter("always")
    with warnings.catch_warnings(action="ignore"):
        warnings.warn("inside")
    warnings.warn("outside")
assert [str(item.message) for item in outer] == ["outside"], outer

# nested catch_warnings restores the prior filter state
with warnings.catch_warnings(record=True) as outer:
    warnings.simplefilter("always")
    with warnings.catch_warnings(action="ignore"):
        with warnings.catch_warnings(action="always"):
            warnings.warn("inner")
        warnings.warn("outer")
    warnings.warn("after")
assert [str(item.message) for item in outer] == ["inner", "after"], outer

# message regex filter
with warnings.catch_warnings(record=True) as records:
    warnings.filterwarnings("ignore", message="boom")
    warnings.warn("boom")
    warnings.warn("other")
assert [str(item.message) for item in records] == ["other"], records

# regex matching is case-insensitive
with warnings.catch_warnings(record=True) as records:
    warnings.filterwarnings("ignore", message="BOOM")
    warnings.warn("boom")
assert len(records) == 0, records

# filterwarnings validation errors
try:
    warnings.filterwarnings("bogus")
    raise AssertionError("invalid action should raise ValueError")
except ValueError:
    pass

try:
    warnings.filterwarnings("ignore", message=123)
    raise AssertionError("non-string message should raise TypeError")
except TypeError:
    pass

try:
    warnings.filterwarnings("ignore", lineno=-1)
    raise AssertionError("negative lineno should raise ValueError")
except ValueError:
    pass

try:
    warnings.filterwarnings("ignore", lineno="x")
    raise AssertionError("non-int lineno should raise TypeError")
except TypeError:
    pass

try:
    warnings.filterwarnings("ignore", category=int)
    raise AssertionError("invalid category should raise TypeError")
except TypeError:
    pass

# resetwarnings clears filters in the active warnings state
with warnings.catch_warnings(record=True) as records:
    warnings.filterwarnings("ignore")
    warnings.resetwarnings()
    warnings.warn("boom")
assert len(records) == 1, records
assert str(records[0].message) == "boom", records[0].message

print("test_warnings_module_filters passed")

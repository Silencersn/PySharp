# A package importing its own submodule from __init__ must work: the package
# object is already visible to the imports its body performs.
VALUE = 42
from . import sub
from .sub import HELPER

HELPER_FROM_INIT = HELPER
B = sub.B

"""Module-level annotations follow PEP 649: the module body stores an
__annotate__ payload, evaluation happens at the first __annotations__ read
(side effects fire then, not at definition time), and the bare name stays
undefined inside the module scope.

:kind: test
"""

import module_annotations_mod as m
import module_annotations_empty_mod as empty
import module_annotations_bad_mod as bad

# inside the annotated module's own scope the name is still unbound
m.in_scope_checks()

# the first read evaluates every reached site in source order and caches;
# the side effect fires here, not at definition time
anns = m.__annotations__
print(anns["z"] is float, anns["w"] is int, anns["taken"] is bytes)

# conditional sites follow execution across control flow: the loop and the
# taken handler count, the untaken branch and the raising try body do not
print("loop_site" in anns, "handler_site" in anns, "skipped" in anns)

# the cache identity: a second read returns the same dict
print("cached:", anns is m.__annotations__)

# assigning __annotations__ detaches the lazy evaluator (PEP 749)
m.__annotations__ = {"x": 1}
print(m.__annotations__)

# a module without annotations materializes an empty dict
print(empty.__annotations__)

# annotation evaluation errors propagate to the reader, and a failed
# evaluation is not cached — every read raises again
try:
    bad.__annotations__
    print("FAIL: no NameError")
except NameError:
    print("NameError")
try:
    bad.__annotations__
    print("FAIL: no NameError")
except NameError:
    print("NameError")

# a malformed __annotate__ payload in the module dict degrades to an empty
# dict on read, like CPython's non-callable face. Each case first deletes
# the cached dict so the read actually reaches the payload branch (the
# write goes through __dict__ because CPython's __annotate__ setter
# requires callable/None)
del empty.__annotations__
empty.__dict__["__annotate__"] = (1, 2)
print(empty.__annotations__)
del empty.__annotations__
empty.__dict__["__annotate__"] = "junk"
print(empty.__annotations__)

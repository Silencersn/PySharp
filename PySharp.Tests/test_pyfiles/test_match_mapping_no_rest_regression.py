"""
Regression: a mapping pattern (`case {...}`) that matches successfully
without a `**rest` capture must not leave the keys tuple on the operand
stack. The compiler used to skip popping the keys tuple on the success
path, breaking the "EmitPattern leaves the stack unchanged" contract.

CPython 3.14 reference (Python/codegen.c codegen_pattern_mapping): when
there is no `**rest`, the keys tuple and the subject are explicitly
popped (POP_TOP x2) before jumping to the matched label.

Observable failure modes before the fix:
- at a code object end (module / function body): VM assertion
  "Stack.Count is greater than 0 when a code object runs to the end"
  terminates the process on Debug builds;
- inside a loop: the residue overwrites the loop's iterator slot ->
  "TypeError: iter() returned non-iterator of type 'dict'".

The loop form is exercised here (deterministic Python-level error that
does not kill the test host). The end-of-body form shares the same root
cause and is intentionally not executed in-process.
"""

# inside a loop: must complete both iterations normally
for _ in range(2):
    match {"a": 1}:
        case {"a": v}:
            assert v == 1

print("loop done")

# --- guards: stack-balanced forms that must keep working ---

# **rest present: the rest branch consumes the keys tuple
match {"a": 1}:
    case {"a": v, **rest}:
        assert v == 1
        assert rest == {}
    case _:
        assert False, "should match"

# failed match: non-dict subject never touches the success path
match [1]:
    case {"a": v}:
        assert False, "should not match"
    case _:
        pass

# mapping pattern with only **rest
match {"x": 9}:
    case {**rest}:
        assert rest == {"x": 9}
    case _:
        assert False, "should match"

print("test_match_mapping_no_rest_regression passed")

"""Verifies that __exit__ receives (None, None, None) on normal with-body completion and the active exception on failure, and that a truthy __exit__ return suppresses the exception.

:kind: test
"""

class A:
    def __enter__(self):
        pass

    def __exit__(self, exc_type, exc_val, exc_tb):
        assert exc_type is None
        assert exc_val is None
        assert exc_tb is None


class B:
    def __enter__(self):
        pass

    def __exit__(self, exc_type, exc_val, exc_tb):
        assert issubclass(exc_type, ValueError)
        assert isinstance(exc_val, ValueError)
        return True


with A(), B():
    raise ValueError
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
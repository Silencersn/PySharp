class __Outer:
    __value = 10
    __special__ = 11

    def __method(self, __arg):
        return __arg + self.__value

    class __Inner:
        __inner = 7

        def get(self):
            return self.__inner


outer = __Outer()
assert __Outer._Outer__value == 10
assert __Outer.__special__ == 11
assert outer._Outer__method(2) == 12

inner = __Outer._Outer__Inner()
assert inner._Inner__inner == 7
assert inner.get() == 7

class ___:
    __v = 3

assert ___.__v == 3

class Kw:
    v = dict(__x=1)["_Kw__x"]

assert Kw.v == 1

class ImportInClass:
    import __mod_mangle
    imported = _ImportInClass__mod_mangle.value

assert ImportInClass.imported == 123

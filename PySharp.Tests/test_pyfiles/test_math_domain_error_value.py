"""math domain errors render the converted double as a repr: an int reads with .0, an object goes through __float__, and big values keep the shortest form.

:kind: test
"""

# Regression: math_1 formats its domain error with the double returned
# by the conversion (Py_DTSF_ADD_DOT_0), not the original argument's
# repr — an int argument reads -1.0 instead of -1, an object with
# __float__ reads its converted value instead of a default object repr,
# and a too-large int keeps the shortest scientific form.


import math


class F:
    def __init__(self, v):
        self.v = v

    def __float__(self):
        return self.v


def show(label, f):
    try:
        print(label, "=", repr(f()))
    except BaseException as e:
        print(label, "!", type(e).__name__, repr(str(e)))


show("sqrt_int", lambda: math.sqrt(-1))
show("asin_int", lambda: math.asin(2))
show("acos_int", lambda: math.acos(2))
show("atanh_int", lambda: math.atanh(2))
show("acosh_int", lambda: math.acosh(0))
show("log1p_int", lambda: math.log1p(-1))
show("cos_inf", lambda: math.cos(float('inf')))
show("sin_nan", lambda: math.sin(float('nan')))
show("tan_inf", lambda: math.tan(float('inf')))
show("sqrt_float", lambda: math.sqrt(-1.0))
show("asin_float", lambda: math.asin(2.0))
show("acosh_float", lambda: math.acosh(0.0))
show("asin_obj", lambda: math.asin(F(2.0)))
show("sqrt_obj", lambda: math.sqrt(F(-1.0)))
show("cos_obj_inf", lambda: math.cos(F(float('inf'))))
show("asin_bignum", lambda: math.asin(10**20))
show("sqrt_bignum_neg", lambda: math.sqrt(-10**20))
show("log_int_neg", lambda: math.log(0))
show("log2_bignum_neg", lambda: math.log2(-10**20))
show("log1p_obj", lambda: math.log1p(F(-2.5)))
show("atanh_obj", lambda: math.atanh(F(2.0)))
show("atanh_float", lambda: math.atanh(-1.0))
show("log_float_neg", lambda: math.log(-2.5))

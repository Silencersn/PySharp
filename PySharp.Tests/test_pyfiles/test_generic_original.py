"""
Original user example: class C[T] with method test[K] referencing both T and K.
"""
print("testing original generic example")

class C[T]:
    def test[K](self):
        a = T, K
        print(a)

c = C()
c.test()

print("test_generic_original passed")

using System.Buffers;
using System.Diagnostics;
using System.Numerics;

namespace PySharp.PyModules.Random;

partial class PyRandomObject
{
    private BigInteger InternalRandom(BigInteger stop)
    {
        // [0, stop)
        Debug.Assert(stop > 0);

        if (stop < long.MaxValue)
            return _random.NextInt64((long)stop);

        const int MaxStackLimit = 1024;

        var count = stop.GetByteCount();
        byte[]? rentedArray = null;
        Span<byte> bytes = count <= MaxStackLimit ? stackalloc byte[count] : (rentedArray = ArrayPool<byte>.Shared.Rent(count)).AsSpan(0, count);
        while (true)
        {
            _random.NextBytes(bytes);
            var result = new BigInteger(bytes, true);
            Debug.Assert(result >= 0);
            if (result < stop)
            {
                if (rentedArray is not null)
                    ArrayPool<byte>.Shared.Return(rentedArray);
                return result;
            }
        }
    }

    public double PyRandom()
    {
        return _random.NextDouble();
    }

    public double PyUniform(double a, double b)
    {
        return a + (b - a) * PyRandom();
    }

    public BigInteger? PyRandRange(BigInteger start, BigInteger stop, BigInteger step)
    {
        if (step == 0)
            return null;

        BigInteger count = 0;
        if (step > 0 && start < stop)
            count = (stop - start + step - 1) / step;
        else if (step < 0 && start > stop)
            count = (start - stop - step - 1) / -step;

        if (count == 0)
            return null;

        return start + step * InternalRandom(count);
    }

    public BigInteger? PyRandInt(BigInteger a, BigInteger b)
    {
        return PyRandRange(a, b + 1, BigInteger.One);
    }
}

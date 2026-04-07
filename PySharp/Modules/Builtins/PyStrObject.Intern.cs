using PySharp.Runtime;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.Modules.Builtins;

partial class PyStrObject
{
    public sealed class InternPool
    {
        private static readonly FrozenDictionary<string, PyStrObject> _staticInternedStrings;

        static InternPool()
        {
            _staticInternedStrings = PySpecialNames.EnumerateNonGeneratedNames()
                .Concat(PySpecialNames.EnumerateGeneratedNames())
                .ToFrozenDictionary(static name => name, PyStrObject.FromString);
        }

        internal static bool TryGetStaticInternedString(ReadOnlySpan<char> value, [NotNullWhen(true)] out string? internedString)
        {
            if (_staticInternedStrings.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(value, out var internedStr))
            {
                internedString = internedStr.Value;
                return true;
            }

            internedString = null;
            return false;
        }

        public static PyStrObject FromString(ReadOnlySpan<char> value)
        {
            if (value.Length is 0)
                return Empty;

            if (value.Length is 1 && value[0] < CharPoolSize)
                return _charPool[value[0]];

            if (_staticInternedStrings.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(value, out var internedStr))
                return internedStr;

            return new PyStrObject(value.ToString());
        }

        public static PyStrObject FromString(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (value.Length is 0)
                return Empty;

            if (value.Length is 1 && value[0] < CharPoolSize)
                return _charPool[value[0]];

            if (_staticInternedStrings.TryGetValue(value, out var internedStr))
                return internedStr;

            return new PyStrObject(value);
        }

        private readonly ConcurrentDictionary<string, PyStrObject> _internedStrings;

        internal InternPool()
        {
            _internedStrings = [];
        }

        internal PyStrObject? TryGetInternedString(string value)
        {
            if (value.Length is 0)
                return Empty;

            if (value.Length is 1 && value[0] < CharPoolSize)
                return _charPool[value[0]];

            if (_staticInternedStrings.TryGetValue(value, out var internedStr))
                return internedStr;

            if (_internedStrings.TryGetValue(value, out internedStr))
                return internedStr;

            return null;
        }

        public PyStrObject Intern(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            var internedStr = TryGetInternedString(value);
            if (internedStr is not null)
                return internedStr;

            return _internedStrings[value] = new PyStrObject(value);
        }

        public PyStrObject GetInternedOrNew(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            var internedStr = TryGetInternedString(value);
            return internedStr ?? new PyStrObject(value);
        }
    }
}

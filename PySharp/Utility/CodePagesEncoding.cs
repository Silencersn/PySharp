using System.Runtime.CompilerServices;
using System.Text;

namespace PySharp.Utility;

// .NET exposes the Windows code pages (gbk/cp1252/big5/...) only after the
// provider is registered; source decoding (PEP 263) and the str/bytes
// codecs both resolve names through here
internal static class CodePagesEncoding
{
    static CodePagesEncoding() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void EnsureRegistered()
    {
        // calling in ensures the static constructor has run; NoInlining
        // keeps the type initialization trigger from being optimized away
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PySharp.SourceGeneration.Diagnostics;

/// <summary>
/// A union of a value of type <typeparamref name="T"/> and zero or more
/// <see cref="DiagnosticInfo"/>s. Simplified from dotnet/runtime's
/// <c>DiagnosticOr&lt;T&gt;</c> (Microsoft.Interop.SourceGeneration), keeping only the two
/// variants this codebase needs: a value or a diagnostic. A null result (not represented here)
/// means the argument was undecodable and the caller should fall back silently.
/// </summary>
internal sealed record DiagnosticOr<T> where T : class
{
    /// <summary>True when this carries a value.</summary>
    public bool HasValue { get; }

    /// <summary>True when this carries at least one diagnostic.</summary>
    public bool HasDiagnostic { get; }

    /// <summary>Throws <see cref="InvalidOperationException"/> if <see cref="HasValue"/> is false.</summary>
    public T Value => HasValue ? _value! : throw new InvalidOperationException();

    /// <summary>Throws <see cref="InvalidOperationException"/> if <see cref="HasDiagnostic"/> is false.</summary>
    public ImmutableArray<DiagnosticInfo> Diagnostics => HasDiagnostic ? _diagnostics : throw new InvalidOperationException();

    private readonly T? _value;
    private readonly ImmutableArray<DiagnosticInfo> _diagnostics;

    private DiagnosticOr(T? value, ImmutableArray<DiagnosticInfo> diagnostics, bool hasValue, bool hasDiagnostic)
    {
        _value = value;
        _diagnostics = diagnostics;
        HasValue = hasValue;
        HasDiagnostic = hasDiagnostic;
    }

    /// <summary>Creates a value variant.</summary>
    public static DiagnosticOr<T> From(T value) => new(value, default, hasValue: true, hasDiagnostic: false);

    /// <summary>Creates a diagnostic variant from a single error.</summary>
    public static DiagnosticOr<T> From(DiagnosticInfo diagnostic) => new(default, [diagnostic], hasValue: false, hasDiagnostic: true);

    /// <summary>Creates a diagnostic variant from multiple errors.</summary>
    public static DiagnosticOr<T> From(IEnumerable<DiagnosticInfo> diagnostics) => new(default, [.. diagnostics], hasValue: false, hasDiagnostic: true);
}

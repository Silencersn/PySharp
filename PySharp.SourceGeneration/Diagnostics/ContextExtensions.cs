using System.Collections.Immutable;

namespace PySharp.SourceGeneration.Diagnostics;

internal static class ContextExtensions
{
    public static void ReportAll(this SourceProductionContext context, ImmutableArray<DiagnosticInfo> diagnosticInfos)
    {
        foreach (var diagnosticInfo in diagnosticInfos)
            context.ReportDiagnostic(diagnosticInfo.ToDiagnostic());
    }

    public static void Report(this SourceProductionContext context, DiagnosticInfo diagnosticInfo)
    {
        context.ReportDiagnostic(diagnosticInfo.ToDiagnostic());
    }
}

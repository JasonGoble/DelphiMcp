namespace DelphiMcp.Parsing;

public sealed class DelphiDiagnosticsPipelineResult
{
    public IReadOnlyList<DelphiParserDiagnostic> Diagnostics { get; init; } = [];
    public int TotalCount { get; init; }
    public int ReturnedCount { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int InfoCount { get; init; }
    public bool Truncated { get; init; }
}

public sealed class DelphiDiagnosticsPipeline
{
    public DelphiDiagnosticsPipelineResult Build(IEnumerable<DelphiParserDiagnostic> diagnostics, int maxDiagnostics = 500)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (maxDiagnostics <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDiagnostics), "maxDiagnostics must be greater than zero.");
        }

        var distinct = diagnostics
            .GroupBy(CreateKey, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderByDescending(d => SeverityRank(d.Severity))
            .ThenBy(d => d.Span.StartLine)
            .ThenBy(d => d.Span.StartColumn)
            .ThenBy(d => d.Code, StringComparer.Ordinal)
            .ToList();

        var returned = distinct.Take(maxDiagnostics).ToList();

        return new DelphiDiagnosticsPipelineResult
        {
            Diagnostics = returned,
            TotalCount = distinct.Count,
            ReturnedCount = returned.Count,
            ErrorCount = distinct.Count(d => d.Severity == DelphiDiagnosticSeverity.Error),
            WarningCount = distinct.Count(d => d.Severity == DelphiDiagnosticSeverity.Warning),
            InfoCount = distinct.Count(d => d.Severity == DelphiDiagnosticSeverity.Info),
            Truncated = distinct.Count > maxDiagnostics
        };
    }

    private static string CreateKey(DelphiParserDiagnostic diagnostic)
    {
        return string.Join('|',
            diagnostic.Code,
            diagnostic.Severity,
            diagnostic.Message,
            diagnostic.Span.StartLine,
            diagnostic.Span.StartColumn,
            diagnostic.Span.EndLine,
            diagnostic.Span.EndColumn,
            diagnostic.RecoveryHint ?? string.Empty);
    }

    private static int SeverityRank(DelphiDiagnosticSeverity severity) => severity switch
    {
        DelphiDiagnosticSeverity.Error => 3,
        DelphiDiagnosticSeverity.Warning => 2,
        DelphiDiagnosticSeverity.Info => 1,
        _ => 0
    };
}
using DelphiMcp.Parsing;

namespace DelphiMcp.Tests;

public class DelphiDiagnosticsPipelineTests
{
    [Fact]
    public void Build_DeduplicatesAndOrdersDiagnostics()
    {
        var diagnostics = new[]
        {
            new DelphiParserDiagnostic
            {
                Code = "DP2",
                Severity = DelphiDiagnosticSeverity.Warning,
                Message = "Warning",
                Span = new DelphiSourceSpan(3, 1, 3, 5)
            },
            new DelphiParserDiagnostic
            {
                Code = "DP1",
                Severity = DelphiDiagnosticSeverity.Error,
                Message = "Error",
                Span = new DelphiSourceSpan(2, 1, 2, 5)
            },
            new DelphiParserDiagnostic
            {
                Code = "DP2",
                Severity = DelphiDiagnosticSeverity.Warning,
                Message = "Warning",
                Span = new DelphiSourceSpan(3, 1, 3, 5)
            }
        };

        var pipeline = new DelphiDiagnosticsPipeline();
        var result = pipeline.Build(diagnostics);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.ReturnedCount);
        Assert.Equal("DP1", result.Diagnostics[0].Code);
        Assert.Equal("DP2", result.Diagnostics[1].Code);
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(1, result.WarningCount);
    }

    [Fact]
    public void Build_RespectsMaxDiagnosticsAndMarksTruncated()
    {
        var diagnostics = Enumerable.Range(1, 5)
            .Select(i => new DelphiParserDiagnostic
            {
                Code = $"DP{i}",
                Severity = DelphiDiagnosticSeverity.Info,
                Message = $"Info {i}",
                Span = new DelphiSourceSpan(i, 1, i, 2)
            });

        var pipeline = new DelphiDiagnosticsPipeline();
        var result = pipeline.Build(diagnostics, maxDiagnostics: 3);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.ReturnedCount);
        Assert.True(result.Truncated);
    }

    [Fact]
    public void StructuralParser_UsesDiagnosticsPipeline_ForMissingUnitName()
    {
        const string src = "interface uses System.SysUtils; implementation end.";
        var parser = new DelphiStructuralParser();

        var result = parser.Parse(src);

        Assert.Single(result.Diagnostics, d => d.Code == "DP3001");
        Assert.Equal(1, result.DiagnosticsReport.WarningCount);
        Assert.Equal(1, result.DiagnosticsReport.TotalCount);
        Assert.False(result.DiagnosticsReport.Truncated);
    }
}
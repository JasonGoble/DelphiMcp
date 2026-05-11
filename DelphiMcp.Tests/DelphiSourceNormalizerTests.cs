using DelphiMcp.Parsing;

namespace DelphiMcp.Tests;

public class DelphiSourceNormalizerTests
{
    [Fact]
    public void Normalize_IsDeterministic_ForEquivalentWhitespace()
    {
        const string srcA = "unit UTest; interface uses System.SysUtils; implementation end.";
        const string srcB = "unit   UTest;\ninterface\nuses   System.SysUtils ;\nimplementation\nend.";

        var normalizer = new DelphiSourceNormalizer();

        var a = normalizer.Normalize(srcA);
        var b = normalizer.Normalize(srcB);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Normalize_PreservesDirectivesAsStandaloneLines()
    {
        const string src = "unit U; interface {$IFDEF DEBUG} procedure Run; {$ENDIF} implementation end.";
        var normalizer = new DelphiSourceNormalizer();

        var normalized = normalizer.Normalize(src);

        Assert.Contains("{$IFDEF DEBUG}", normalized, StringComparison.Ordinal);
        Assert.Contains("{$ENDIF}", normalized, StringComparison.Ordinal);
        Assert.Contains("\n{$IFDEF DEBUG}\n", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_DropsComments_WhenRequested()
    {
        const string src = "unit U; // unit comment\ninterface {block comment}\nimplementation end.";
        var normalizer = new DelphiSourceNormalizer();

        var normalized = normalizer.Normalize(src, includeComments: false);

        Assert.DoesNotContain("unit comment", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("block comment", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("interface", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_UsesUnixLineEndingsAndNoTrailingBlankLines()
    {
        const string src = "unit U;\r\ninterface\r\nimplementation\r\nend.\r\n\r\n";
        var normalizer = new DelphiSourceNormalizer();

        var normalized = normalizer.Normalize(src);

        Assert.DoesNotContain("\r\n", normalized, StringComparison.Ordinal);
        Assert.False(normalized.EndsWith("\n", StringComparison.Ordinal));
    }
}
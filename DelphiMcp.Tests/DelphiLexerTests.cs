using DelphiMcp.Parsing;

namespace DelphiMcp.Tests;

public class DelphiLexerTests
{
    [Fact]
    public void Lex_BasicUnit_ProducesIdentifierAndDirectiveTokens()
    {
        var src = "unit UTest;\ninterface\n{$IFDEF DEBUG}\nprocedure Run;\n{$ENDIF}\nimplementation\nend.";
        var lexer = new DelphiLexer();

        var result = lexer.Lex(src);

        Assert.NotEmpty(result.Tokens);
        Assert.Contains(result.Tokens, t => t.Kind == DelphiTokenKind.Identifier && t.Text.Equals("unit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Tokens, t => t.Kind == DelphiTokenKind.Directive && t.Text.Contains("IFDEF", StringComparison.OrdinalIgnoreCase));
        Assert.Single(result.DirectiveRegions);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Lex_NestedConditionalDirectives_ProducesResolvedRegions()
    {
        var src = "{$IFDEF A}\n{$IFNDEF B}\nprocedure X;\n{$ENDIF}\n{$ENDIF}";
        var lexer = new DelphiLexer();

        var result = lexer.Lex(src);

        Assert.Equal(2, result.DirectiveRegions.Count);
        Assert.All(result.DirectiveRegions, r => Assert.True(r.IsResolved));
        Assert.Contains(result.DirectiveRegions, r => r.Directive == "IFDEF" && r.Condition == "A");
        Assert.Contains(result.DirectiveRegions, r => r.Directive == "IFNDEF" && r.Condition == "B");
    }

    [Fact]
    public void Lex_UnmatchedEndif_ProducesDiagnostic()
    {
        var src = "unit U;\n{$ENDIF}\nend.";
        var lexer = new DelphiLexer();

        var result = lexer.Lex(src);

        Assert.Contains(result.Diagnostics, d => d.Code == "DP2001");
    }

    [Fact]
    public void Lex_UnclosedConditionalStart_ProducesDiagnostic()
    {
        var src = "unit U;\n{$IFDEF DEBUG}\nprocedure Test;\nend.";
        var lexer = new DelphiLexer();

        var result = lexer.Lex(src);

        Assert.Contains(result.Diagnostics, d => d.Code == "DP2002");
    }

    [Fact]
    public void Lex_ParensDirectiveSyntax_IsHandled()
    {
        var src = "(*$IFDEF RELEASE*)\nprocedure Prod;\n(*$ENDIF*)";
        var lexer = new DelphiLexer();

        var result = lexer.Lex(src);

        Assert.Single(result.DirectiveRegions);
        Assert.Empty(result.Diagnostics);
        Assert.Contains(result.Tokens, t => t.Kind == DelphiTokenKind.Directive && t.Text.StartsWith("(*$IFDEF", StringComparison.Ordinal));
    }
}
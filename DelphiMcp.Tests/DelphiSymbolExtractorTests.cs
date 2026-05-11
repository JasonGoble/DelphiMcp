using DelphiMcp.Parsing;

namespace DelphiMcp.Tests;

public class DelphiSymbolExtractorTests
{
    [Fact]
    public void Extract_IncludesUnitAndTypeSymbols()
    {
        const string src = """
        unit USymbols;
        interface
        type
          TFoo = class
          end;

          TBar = record
          end;

          IZap = interface
          end;
        implementation
        end.
        """;

        var parser = new DelphiStructuralParser();
        var parsed = parser.Parse(src);
        var extractor = new DelphiSymbolExtractor();

        var symbols = extractor.Extract(parsed.AstSummary);

        Assert.Contains(symbols, s => s.Kind == DelphiSymbolKind.Unit && s.Name == "USymbols");
        Assert.Contains(symbols, s => s.Kind == DelphiSymbolKind.Class && s.Name == "TFoo");
        Assert.Contains(symbols, s => s.Kind == DelphiSymbolKind.Record && s.Name == "TBar");
        Assert.Contains(symbols, s => s.Kind == DelphiSymbolKind.Interface && s.Name == "IZap");
    }

    [Fact]
    public void Extract_UsesAstUnitNameOnAllExtractedSymbols()
    {
        const string src = """
        unit UScoped;
        interface
        type
          TFoo = class
          end;
        implementation
        end.
        """;

        var parser = new DelphiStructuralParser();
        var parsed = parser.Parse(src);
        var extractor = new DelphiSymbolExtractor();

        var symbols = extractor.Extract(parsed.AstSummary);

        Assert.NotEmpty(symbols);
        Assert.All(symbols, s => Assert.Equal("UScoped", s.UnitName));
    }

    [Fact]
    public void Extract_BuildsStableTypeSignatures()
    {
        var ast = new DelphiAstSummary
        {
            UnitName = "UTest",
            Nodes =
            [
                new DelphiAstNode
                {
                    Id = "n1",
                    Kind = DelphiNodeKind.Unit,
                    Name = "UTest"
                },
                new DelphiAstNode
                {
                    Id = "n2",
                    ParentId = "n1",
                    Kind = DelphiNodeKind.ClassDeclaration,
                    Name = "TFoo"
                }
            ]
        };

        var extractor = new DelphiSymbolExtractor();
        var symbols = extractor.Extract(ast);

        var foo = Assert.Single(symbols, s => s.Name == "TFoo");
        Assert.Equal("TFoo = class;", foo.Signature);
    }
}
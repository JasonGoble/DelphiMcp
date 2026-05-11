using DelphiMcp.Parsing;

namespace DelphiMcp.Tests;

public class DelphiStructuralParserTests
{
    [Fact]
    public void Parse_ExtractsUnitNameAndSections()
    {
        const string src = """
        unit UTest;

        interface
        uses System.SysUtils;

        implementation
        uses Vcl.Forms;

        initialization
        finalization
        end.
        """;

        var parser = new DelphiStructuralParser();
        var result = parser.Parse(src);

        Assert.Equal("UTest", result.AstSummary.UnitName);
        Assert.Contains(result.AstSummary.Sections, s => s.SectionName == "interface");
        Assert.Contains(result.AstSummary.Sections, s => s.SectionName == "implementation");
        Assert.Contains(result.AstSummary.Sections, s => s.SectionName == "initialization");
        Assert.Contains(result.AstSummary.Sections, s => s.SectionName == "finalization");
    }

    [Fact]
    public void Parse_ExtractsUsesPerSection()
    {
        const string src = """
        unit UTest;
        interface
        uses System.SysUtils, System.Classes;
        implementation
        uses Vcl.Forms in 'Vcl.Forms.pas', Vcl.Controls;
        end.
        """;

        var parser = new DelphiStructuralParser();
        var result = parser.Parse(src);

        var interfaceSection = Assert.Single(result.AstSummary.Sections, s => s.SectionName == "interface");
        var implSection = Assert.Single(result.AstSummary.Sections, s => s.SectionName == "implementation");

        Assert.Contains("System.SysUtils", interfaceSection.UsesUnits);
        Assert.Contains("System.Classes", interfaceSection.UsesUnits);
        Assert.Contains("Vcl.Forms", implSection.UsesUnits);
        Assert.Contains("Vcl.Controls", implSection.UsesUnits);
    }

    [Fact]
    public void Parse_ExtractsClassRecordInterfaceTypeDeclarations()
    {
        const string src = """
        unit UTypes;
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
        var result = parser.Parse(src);

        Assert.Contains(result.AstSummary.Nodes, n => n.Kind == DelphiNodeKind.ClassDeclaration && n.Name == "TFoo");
        Assert.Contains(result.AstSummary.Nodes, n => n.Kind == DelphiNodeKind.RecordDeclaration && n.Name == "TBar");
        Assert.Contains(result.AstSummary.Nodes, n => n.Kind == DelphiNodeKind.InterfaceDeclaration && n.Name == "IZap");
    }

    [Fact]
    public void Parse_MissingUnitName_ProducesDiagnostic()
    {
        const string src = "interface uses System.SysUtils; implementation end.";

        var parser = new DelphiStructuralParser();
        var result = parser.Parse(src);

        Assert.Contains(result.Diagnostics, d => d.Code == "DP3001");
        Assert.Equal("<unknown-unit>", result.AstSummary.Nodes.First().Name);
    }
}
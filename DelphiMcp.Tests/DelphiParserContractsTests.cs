using System.Text.Json;
using DelphiMcp.Parsing;

namespace DelphiMcp.Tests;

public class DelphiParserContractsTests
{
    [Fact]
    public void ParseRequest_Defaults_AreStable()
    {
        var request = new ParseDelphiStructureRequest
        {
            SourceText = "unit UTest; interface implementation end."
        };

        Assert.Equal(5000, request.MaxNodeCount);
        Assert.Equal(2000, request.MaxSymbolCount);
        Assert.False(request.IncludeTrivia);
        Assert.Equal(2, request.OutputModes.Count);
        Assert.Contains(DelphiStructureOutputMode.AstSummary, request.OutputModes);
        Assert.Contains(DelphiStructureOutputMode.Diagnostics, request.OutputModes);
    }

    [Fact]
    public void ParseResponse_DefaultSchemaVersion_IsExpected()
    {
        var response = new ParseDelphiStructureResponse
        {
            Request = new ParseDelphiStructureRequestContext()
        };

        Assert.Equal("2.0-draft1", response.SchemaVersion);
        Assert.Equal(5000, response.AppliedLimits.MaxNodeCount);
        Assert.Equal(2000, response.AppliedLimits.MaxSymbolCount);
        Assert.Equal(500, response.AppliedLimits.MaxDiagnostics);
    }

    [Fact]
    public void Contracts_SerializeEnums_AsStrings()
    {
        var response = new ParseDelphiStructureResponse
        {
            Request = new ParseDelphiStructureRequestContext
            {
                SourcePath = "rtl/System.pas",
                Library = "rtl",
                Version = "13.1",
                OutputModes =
                [
                    DelphiStructureOutputMode.AstSummary,
                    DelphiStructureOutputMode.SymbolTable
                ]
            },
            AstSummary = new DelphiAstSummary
            {
                UnitName = "System",
                Nodes =
                [
                    new DelphiAstNode
                    {
                        Id = "n1",
                        Kind = DelphiNodeKind.Unit,
                        Name = "System"
                    }
                ]
            },
            SymbolTable =
            [
                new DelphiSymbolDescriptor
                {
                    Name = "TObject",
                    Kind = DelphiSymbolKind.Class,
                    UnitName = "System"
                }
            ],
            Diagnostics =
            [
                new DelphiParserDiagnostic
                {
                    Code = "DP1001",
                    Severity = DelphiDiagnosticSeverity.Warning,
                    Message = "Unresolved IFDEF symbol"
                }
            ]
        };

        var json = JsonSerializer.Serialize(response);

        Assert.Contains("\"AstSummary\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Unit\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Class\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Warning\"", json, StringComparison.Ordinal);
    }
}
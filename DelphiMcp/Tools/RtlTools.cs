using DelphiMcp.Search;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace DelphiMcp.Tools;

[McpServerToolType]
public static class RtlTools
{
    private const string Library = "rtl";

    [McpServerTool(Name = "search_rtl"), Description(
        "Search the Delphi RTL/VCL/FMX source for types, routines, and implementations " +
        "relevant to a query. Returns source excerpts with unit names and line numbers.")]
    public static async Task<string> SearchRtl(
        [Description("Natural language query or symbol name, e.g. 'TStringList thread safety' or 'file stream buffering'")]
        string query,
        DelphiSearcher searcher,
        [Description("Number of results to return (default 5, max 10)")]
        int topK = 5,
        [Description("Optional Delphi version to filter, e.g. '12.0' or '11.0'. Omit to search across all indexed versions.")]
        string? version = null)
    {
        topK = Math.Clamp(topK, 1, 10);
        return await searcher.SearchAsync(Library, version, query, topK);
    }

    [McpServerTool(Name = "lookup_rtl_class"), Description(
        "Look up the declaration of a specific Delphi RTL/VCL class by name. " +
        "Returns the full class declaration including properties and method signatures.")]
    public static async Task<string> LookupRtlClass(
        [Description("The class name to look up, e.g. 'TStringList', 'TMemoryStream', 'TThread'")]
        string className,
        DelphiSearcher searcher,
        [Description("Optional Delphi version to filter. Omit to look up across all indexed versions.")]
        string? version = null)
    {
        return await searcher.LookupClassAsync(Library, version, className);
    }
}

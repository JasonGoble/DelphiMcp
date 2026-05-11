using DelphiMcp.Search;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace DelphiMcp.Tools;

[McpServerToolType]
[Obsolete("v1.2: Use unified tool 'search_delphi_source' with library='RTL' instead. See README v1.1→v1.2 Migration Guide.", false)]
public static class RtlTools
{
    private const string Library = "rtl";

    [McpServerTool(Name = "search_rtl"), Description(
        "[DEPRECATED in v1.2] Search the Delphi RTL/VCL/FMX source for types, routines, and implementations " +
        "relevant to a query. Returns source excerpts with unit names and line numbers. " +
        "MIGRATE TO: search_delphi_source with library='RTL'")]
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

    [Obsolete("v1.2: Use unified tool 'lookup_delphi_class' with library='RTL' instead. See README v1.1→v1.2 Migration Guide.", false)]
    [McpServerTool(Name = "lookup_rtl_class"), Description(
        "[DEPRECATED in v1.2] Look up the declaration of a specific Delphi RTL/VCL class by name. " +
        "Returns the full class declaration including properties and method signatures. " +
        "MIGRATE TO: lookup_delphi_class with library='RTL'")]
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

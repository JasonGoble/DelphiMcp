using DelphiMcp.Search;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace DelphiMcp.Tools;

[McpServerToolType]
[Obsolete("v1.2: Use unified tool 'search_delphi_source' with library='DevExpress' instead. See README v1.1→v1.2 Migration Guide.", false)]
public static class DevExpressTools
{
    private const string Library = "devexpress";

    [McpServerTool(Name = "search_devexpress"), Description(
        "[DEPRECATED in v1.2] Search the DevExpress VCL component source (cxGrid, ExpressBars, etc.) for types, " +
        "routines, and implementations relevant to a query. Returns source excerpts with unit names and line numbers. " +
        "MIGRATE TO: search_delphi_source with library='DevExpress'")]
    public static async Task<string> SearchDevExpress(
        [Description("Natural language query or symbol name, e.g. 'cxGrid focused row' or 'ExpressBars docking'")]
        string query,
        DelphiSearcher searcher,
        [Description("Number of results to return (default 5, max 10)")]
        int topK = 5,
        [Description("Optional DevExpress version to filter (matches the Delphi version used to build it). Omit to search across all indexed versions.")]
        string? version = null)
    {
        topK = Math.Clamp(topK, 1, 10);
        return await searcher.SearchAsync(Library, version, query, topK);
    }

    [Obsolete("v1.2: Use unified tool 'lookup_delphi_class' with library='DevExpress' instead. See README v1.1→v1.2 Migration Guide.", false)]
    [McpServerTool(Name = "lookup_devexpress_class"), Description(
        "[DEPRECATED in v1.2] Look up the declaration of a specific DevExpress class by name. " +
        "Returns the full class declaration including properties and method signatures. " +
        "MIGRATE TO: lookup_delphi_class with library='DevExpress'")]
    public static async Task<string> LookupDevExpressClass(
        [Description("The class name to look up, e.g. 'TcxGrid', 'TcxGridDBTableView', 'TdxBarManager'")]
        string className,
        DelphiSearcher searcher,
        [Description("Optional DevExpress version to filter. Omit to look up across all indexed versions.")]
        string? version = null)
    {
        return await searcher.LookupClassAsync(Library, version, className);
    }
}

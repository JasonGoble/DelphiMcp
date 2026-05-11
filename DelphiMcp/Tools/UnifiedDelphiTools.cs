using DelphiMcp.ClientAccess;
using DelphiMcp.Search;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace DelphiMcp.Tools;

/// <summary>
/// Unified Delphi source tools supporting both library-scoped and profile-based (unscoped) queries.
/// These tools integrate with the ClientProfileResolver to enforce access policies and scope limits.
/// </summary>
[McpServerToolType]
public static class UnifiedDelphiTools
{
    /// <summary>
    /// Search Delphi source code across RTL, VCL, FMX, DevExpress, and other libraries.
    /// Supports both library-specific searches and profile-based unscoped queries using resolved client profiles.
    /// </summary>
    [McpServerTool(Name = "search_delphi_source"), Description(
        "Search Delphi source code for types, routines, and implementations. " +
        "When library is specified, searches that library; when omitted, uses the resolved client profile's default scopes. " +
        "Results include library/version metadata and source excerpts with line numbers.")]
    public static async Task<string> SearchDelphiSource(
        [Description("Natural language query or symbol name, e.g. 'TStringList thread safety' or 'cxGrid focused row'")]
        string query,
        DelphiSearcher searcher,
        [Description("Optional library name (e.g., 'rtl', 'vcl', 'devexpress'). Omit to use client profile default scopes.")]
        string? library = null,
        [Description("Optional single Delphi/library version to filter (e.g., '12.0' or '11.0'). Omit to search all versions in selected library/scope.")]
        string? version = null,
        [Description("Number of results to return (default 5, respects client profile policy MaxTopK limit)")]
        int topK = 5,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        var profile = httpContextAccessor?.HttpContext?.Items[ClientProfileResolver.HttpContextItemKey] as ResolvedClientProfile;
        var policy = profile?.Policy;

        // Apply policy limits to topK
        if (policy is not null)
        {
            topK = Math.Min(topK, policy.MaxTopK);
        }
        topK = Math.Clamp(topK, 1, 10);

        // If library is specified, perform a scoped search
        if (!string.IsNullOrWhiteSpace(library))
        {
            return await searcher.SearchAsync(library, version, query, topK);
        }

        // If no library specified, use profile default scopes (if available)
        if (profile?.DefaultScopes.Count > 0)
        {
            var results = new List<string>();

            foreach (var scope in profile.DefaultScopes)
            {
                var scopeResult = await searcher.SearchAsync(scope.Library, scope.Version, query, topK);
                results.Add(scopeResult);
            }

            if (results.Count == 0)
                return $"No default scopes configured in client profile.";

            return string.Join("\n---\n", results);
        }

        // No library specified and no profile default scopes; this is an error in HTTP mode
        // In stdio mode (no HttpContext), default to searching across all libraries (all versions)
        if (profile is not null)
        {
            return "Error: library parameter required (no default scopes configured in profile)";
        }

        // Stdio mode fallback: search across all libraries/versions
        // For simplicity, we'll search RTL and DevExpress as the most common libraries
        var allResults = new List<string>();
        foreach (var lib in new[] { "rtl", "devexpress" })
        {
            var libResult = await searcher.SearchAsync(lib, null, query, topK);
            if (!libResult.StartsWith("No relevant"))
            {
                allResults.Add(libResult);
            }
        }

        if (allResults.Count == 0)
            return "No relevant source found for that query across RTL and DevExpress.";

        return string.Join("\n---\n", allResults);
    }

    /// <summary>
    /// Look up the declaration of a specific Delphi class or type by name.
    /// Supports single-version lookup and multi-version comparison (up to policy limit).
    /// </summary>
    [McpServerTool(Name = "lookup_delphi_class"), Description(
        "Look up the declaration of a specific Delphi class or type by name. " +
        "Returns the full declaration including properties and method signatures. " +
        "Optionally compare versions to see differences across multiple Delphi/library versions.")]
    public static async Task<string> LookupDelphiClass(
        [Description("The class name to look up, e.g. 'TStringList', 'TcxGrid', 'TMemoryStream'")]
        string className,
        DelphiSearcher searcher,
        [Description("Optional library name (e.g., 'rtl', 'devexpress'). If omitted, searches across client profile default scopes.")]
        string? library = null,
        [Description("Optional single Delphi/library version to filter. Omit to search all versions in selected library/scope.")]
        string? version = null,
        [Description("Optional list of versions for multi-version comparison (e.g., '12.0,11.0'). Respects MaxVersionsPerLibraryPerQuery policy limit.")]
        string? versions = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        var profile = httpContextAccessor?.HttpContext?.Items[ClientProfileResolver.HttpContextItemKey] as ResolvedClientProfile;
        var policy = profile?.Policy;

        // If a single version is specified, use it
        if (!string.IsNullOrWhiteSpace(version))
        {
            if (string.IsNullOrWhiteSpace(library))
            {
                // No library specified; use first default scope library if available
                if (profile?.DefaultScopes.Count > 0)
                {
                    library = profile.DefaultScopes[0].Library;
                }
                else
                {
                    return "Error: library parameter required when version is specified (no default scopes configured)";
                }
            }

            return await searcher.LookupClassAsync(library, version, className);
        }

        // Handle multi-version comparison
        var versionList = new List<string>();
        if (!string.IsNullOrWhiteSpace(versions))
        {
            versionList = versions.Split(',').Select(v => v.Trim()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        }

        if (versionList.Count > 0)
        {
            // Apply policy limit to number of versions
            int maxVersions = policy?.MaxVersionsPerLibraryPerQuery ?? 2;
            if (versionList.Count > maxVersions)
            {
                return $"Error: requested {versionList.Count} versions but policy allows max {maxVersions} per library";
            }

            if (string.IsNullOrWhiteSpace(library))
            {
                if (profile?.DefaultScopes.Count > 0)
                {
                    library = profile.DefaultScopes[0].Library;
                }
                else
                {
                    return "Error: library parameter required for multi-version comparison (no default scopes configured)";
                }
            }

            var comparisons = new List<string>();
            foreach (var v in versionList)
            {
                var result = await searcher.LookupClassAsync(library, v, className);
                comparisons.Add($"=== Version {v} ===\n{result}");
            }

            return string.Join("\n\n", comparisons);
        }

        // Single version lookup (or default scopes fallback)
        if (!string.IsNullOrWhiteSpace(library))
        {
            // Library specified; lookup across all versions in that library
            return await searcher.LookupClassAsync(library, null, className);
        }

        // No library specified; use profile default scopes
        if (profile?.DefaultScopes.Count > 0)
        {
            var lookups = new List<string>();

            foreach (var scope in profile.DefaultScopes)
            {
                var scopeResult = await searcher.LookupClassAsync(scope.Library, scope.Version, className);
                lookups.Add(scopeResult);
            }

            if (lookups.Count == 0)
                return $"Class '{className}' not found in any default scope.";

            return string.Join("\n---\n", lookups);
        }

        // No library and no profile scopes; fallback
        if (profile is not null)
        {
            return $"Error: library parameter required (no default scopes configured in profile)";
        }

        // Stdio mode fallback: search RTL and DevExpress
        var allLookups = new List<string>();
        foreach (var lib in new[] { "rtl", "devexpress" })
        {
            var libResult = await searcher.LookupClassAsync(lib, null, className);
            if (!libResult.StartsWith("No matching"))
            {
                allLookups.Add(libResult);
            }
        }

        if (allLookups.Count == 0)
            return $"Class '{className}' not found in RTL or DevExpress.";

        return string.Join("\n---\n", allLookups);
    }

    /// <summary>
    /// List available Delphi/library versions for a specific library.
    /// Useful for discovering what versions are indexed and available for queries.
    /// </summary>
    [McpServerTool(Name = "list_delphi_versions"), Description(
        "List available Delphi/library versions that are indexed and available for queries. " +
        "Pass a library name to filter to that library, or omit to see all libraries and their versions.")]
    public static async Task<string> ListDelphiVersions(
        DelphiSearcher searcher,
        [Description("Optional library name to filter (e.g., 'rtl', 'devexpress'). Omit to list all libraries.")]
        string? library = null,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        // This would require adding a method to DelphiSearcher to list available versions
        // For now, return a placeholder that indicates this needs implementation in the store
        return $"ListDelphiVersions is not yet implemented. Available libraries: rtl, devexpress, etc. Please check store/index metadata.";
    }
}

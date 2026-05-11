namespace DelphiMcp.ClientAccess;

public sealed class ClientAccessOptions
{
    public GlobalPolicyOptions GlobalPolicy { get; set; } = new();
    public Dictionary<string, ClientProfileOptions> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class GlobalPolicyOptions
{
    public int MaxTopK { get; set; } = 10;
    public int MaxVersionsPerLibraryPerQuery { get; set; } = 2;
    public int MaxTargetScopesPerQuery { get; set; } = 4;
    public bool AllowUnversionedQueries { get; set; } = true;
    public bool RequireVersionWhenLibrarySpecified { get; set; } = false;
    public string DefaultSearchBehavior { get; set; } = "UseProfileDefaults";
}

public sealed class ClientProfileOptions
{
    public bool Enabled { get; set; } = true;
    public string? DisplayName { get; set; }
    public string? ApiKeyRef { get; set; }
    public string? ApiKey { get; set; }
    public List<TargetScope> DefaultScopes { get; set; } = [];
    public ClientPolicyOverrides Options { get; set; } = new();
}

public sealed class ClientPolicyOverrides
{
    public int? MaxTopK { get; set; }
    public int? MaxVersionsPerLibraryPerQuery { get; set; }
    public int? MaxTargetScopesPerQuery { get; set; }
    public bool? AllowUnversionedQueries { get; set; }
    public bool? RequireVersionWhenLibrarySpecified { get; set; }
}

public sealed class EffectiveClientPolicy
{
    public int MaxTopK { get; init; }
    public int MaxVersionsPerLibraryPerQuery { get; init; }
    public int MaxTargetScopesPerQuery { get; init; }
    public bool AllowUnversionedQueries { get; init; }
    public bool RequireVersionWhenLibrarySpecified { get; init; }
    public string DefaultSearchBehavior { get; init; } = "UseProfileDefaults";
}

public sealed class ResolvedClientProfile
{
    public required string ProfileId { get; init; }
    public required string DisplayName { get; init; }
    public required EffectiveClientPolicy Policy { get; init; }
    public required IReadOnlyList<TargetScope> DefaultScopes { get; init; }
}

public sealed record TargetScope(string Library, string Version);

public sealed record RequestedScope(string Library, string? Version);

public sealed class ScopeResolutionResult
{
    public required IReadOnlyList<TargetScope> Scopes { get; init; }
    public required int EffectiveTopK { get; init; }
    public required bool UsedProfileDefaults { get; init; }
}

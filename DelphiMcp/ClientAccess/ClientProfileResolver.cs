using Microsoft.Extensions.Configuration;

namespace DelphiMcp.ClientAccess;

public sealed class ClientProfileResolver
{
    public const string HttpContextItemKey = "DelphiMcp.ClientProfile";

    private readonly Dictionary<string, ResolvedClientProfile> _profilesByApiKey;

    private ClientProfileResolver(Dictionary<string, ResolvedClientProfile> profilesByApiKey)
    {
        _profilesByApiKey = profilesByApiKey;
    }

    public int EnabledProfileCount => _profilesByApiKey.Count;

    public static ClientProfileResolver FromConfiguration(IConfiguration cfg)
    {
        var options = new ClientAccessOptions();
        cfg.GetSection("ClientAccess").Bind(options);
        return FromOptions(options, cfg);
    }

    internal static ClientProfileResolver FromOptions(ClientAccessOptions options, IConfiguration cfg)
    {
        var global = NormalizePolicy(options.GlobalPolicy);
        var profilesByApiKey = new Dictionary<string, ResolvedClientProfile>(StringComparer.Ordinal);

        foreach (var (profileId, profile) in options.Profiles)
        {
            if (!profile.Enabled)
                continue;

            var apiKey = ResolveApiKey(profileId, profile, cfg);
            if (!profilesByApiKey.TryAdd(apiKey, CreateResolvedProfile(profileId, profile, global)))
                throw new InvalidOperationException($"Duplicate API key resolved for enabled profile '{profileId}'.");
        }

        if (profilesByApiKey.Count == 0)
            throw new InvalidOperationException("ClientAccess:Profiles must include at least one enabled profile.");

        return new ClientProfileResolver(profilesByApiKey);
    }

    public bool TryResolveApiKey(string? apiKey, out ResolvedClientProfile? profile, out string reason)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            profile = null;
            reason = "missing_api_key";
            return false;
        }

        if (_profilesByApiKey.TryGetValue(apiKey.Trim(), out profile))
        {
            reason = "ok";
            return true;
        }

        reason = "unknown_api_key";
        return false;
    }

    public ScopeResolutionResult ResolveScopes(
        ResolvedClientProfile profile,
        IReadOnlyList<RequestedScope>? requestedScopes,
        int requestedTopK,
        Func<string, IReadOnlyList<string>>? availableVersionsProvider = null)
    {
        var effectiveTopK = Math.Clamp(requestedTopK, 1, profile.Policy.MaxTopK);

        if (requestedScopes is null || requestedScopes.Count == 0)
        {
            return new ScopeResolutionResult
            {
                Scopes = profile.DefaultScopes,
                EffectiveTopK = effectiveTopK,
                UsedProfileDefaults = true
            };
        }

        var expandedScopes = new List<TargetScope>();

        foreach (var requested in requestedScopes)
        {
            var library = (requested.Library ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(library))
                throw new InvalidOperationException("Library must be provided for each requested scope.");

            if (!string.IsNullOrWhiteSpace(requested.Version))
            {
                expandedScopes.Add(new TargetScope(library, requested.Version.Trim()));
                continue;
            }

            if (profile.Policy.RequireVersionWhenLibrarySpecified)
                throw new InvalidOperationException($"Version is required when requesting library '{library}'.");

            var defaultVersions = profile.DefaultScopes
                .Where(s => string.Equals(s.Library, library, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Version)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (defaultVersions.Count > 0)
            {
                expandedScopes.AddRange(defaultVersions.Select(v => new TargetScope(library, v)));
                continue;
            }

            if (!profile.Policy.AllowUnversionedQueries)
                throw new InvalidOperationException($"No default version configured for library '{library}'.");

            if (availableVersionsProvider is null)
                throw new InvalidOperationException(
                    $"No default version configured for library '{library}', and version catalog is unavailable.");

            var versions = availableVersionsProvider(library)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(profile.Policy.MaxVersionsPerLibraryPerQuery)
                .ToList();

            if (versions.Count == 0)
                throw new InvalidOperationException($"No indexed versions found for library '{library}'.");

            expandedScopes.AddRange(versions.Select(v => new TargetScope(library, v)));
        }

        var groupedByLibrary = expandedScopes
            .GroupBy(s => s.Library, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                Library = g.Key,
                Versions = g.Select(s => s.Version).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .ToList();

        foreach (var group in groupedByLibrary)
        {
            if (group.Versions.Count > profile.Policy.MaxVersionsPerLibraryPerQuery)
            {
                throw new InvalidOperationException(
                    $"Library '{group.Library}' requested {group.Versions.Count} versions; max is {profile.Policy.MaxVersionsPerLibraryPerQuery}.");
            }
        }

        var normalizedScopes = groupedByLibrary
            .SelectMany(g => g.Versions.Select(v => new TargetScope(g.Library, v)))
            .ToList();

        if (normalizedScopes.Count > profile.Policy.MaxTargetScopesPerQuery)
        {
            throw new InvalidOperationException(
                $"Requested {normalizedScopes.Count} target scopes; max is {profile.Policy.MaxTargetScopesPerQuery}.");
        }

        return new ScopeResolutionResult
        {
            Scopes = normalizedScopes,
            EffectiveTopK = effectiveTopK,
            UsedProfileDefaults = false
        };
    }

    private static ResolvedClientProfile CreateResolvedProfile(
        string profileId,
        ClientProfileOptions profile,
        EffectiveClientPolicy global)
    {
        var defaults = NormalizeDefaultScopes(profileId, profile.DefaultScopes);

        var policy = new EffectiveClientPolicy
        {
            MaxTopK = NormalizePositive(profile.Options.MaxTopK ?? global.MaxTopK, "MaxTopK"),
            MaxVersionsPerLibraryPerQuery = NormalizePositive(
                profile.Options.MaxVersionsPerLibraryPerQuery ?? global.MaxVersionsPerLibraryPerQuery,
                "MaxVersionsPerLibraryPerQuery"),
            MaxTargetScopesPerQuery = NormalizePositive(
                profile.Options.MaxTargetScopesPerQuery ?? global.MaxTargetScopesPerQuery,
                "MaxTargetScopesPerQuery"),
            AllowUnversionedQueries = profile.Options.AllowUnversionedQueries ?? global.AllowUnversionedQueries,
            RequireVersionWhenLibrarySpecified = profile.Options.RequireVersionWhenLibrarySpecified
                ?? global.RequireVersionWhenLibrarySpecified,
            DefaultSearchBehavior = global.DefaultSearchBehavior
        };

        return new ResolvedClientProfile
        {
            ProfileId = profileId,
            DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profileId : profile.DisplayName,
            Policy = policy,
            DefaultScopes = defaults
        };
    }

    private static EffectiveClientPolicy NormalizePolicy(GlobalPolicyOptions policy)
    {
        return new EffectiveClientPolicy
        {
            MaxTopK = NormalizePositive(policy.MaxTopK, "MaxTopK"),
            MaxVersionsPerLibraryPerQuery = NormalizePositive(
                policy.MaxVersionsPerLibraryPerQuery,
                "MaxVersionsPerLibraryPerQuery"),
            MaxTargetScopesPerQuery = NormalizePositive(policy.MaxTargetScopesPerQuery, "MaxTargetScopesPerQuery"),
            AllowUnversionedQueries = policy.AllowUnversionedQueries,
            RequireVersionWhenLibrarySpecified = policy.RequireVersionWhenLibrarySpecified,
            DefaultSearchBehavior = string.IsNullOrWhiteSpace(policy.DefaultSearchBehavior)
                ? "UseProfileDefaults"
                : policy.DefaultSearchBehavior.Trim()
        };
    }

    private static IReadOnlyList<TargetScope> NormalizeDefaultScopes(string profileId, List<TargetScope> scopes)
    {
        if (scopes.Count == 0)
            throw new InvalidOperationException($"Profile '{profileId}' must define at least one DefaultScope.");

        var normalized = new List<TargetScope>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scope in scopes)
        {
            var library = (scope.Library ?? string.Empty).Trim();
            var version = (scope.Version ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(library) || string.IsNullOrWhiteSpace(version))
            {
                throw new InvalidOperationException(
                    $"Profile '{profileId}' has an invalid DefaultScope; both Library and Version are required.");
            }

            var key = $"{library}|{version}";
            if (!seen.Add(key))
                throw new InvalidOperationException($"Profile '{profileId}' contains duplicate DefaultScope '{key}'.");

            normalized.Add(new TargetScope(library, version));
        }

        return normalized;
    }

    private static string ResolveApiKey(string profileId, ClientProfileOptions profile, IConfiguration cfg)
    {
        if (!string.IsNullOrWhiteSpace(profile.ApiKey))
            return profile.ApiKey.Trim();

        if (!string.IsNullOrWhiteSpace(profile.ApiKeyRef))
        {
            var key = cfg[profile.ApiKeyRef.Trim()];
            if (!string.IsNullOrWhiteSpace(key))
                return key.Trim();

            throw new InvalidOperationException(
                $"Profile '{profileId}' ApiKeyRef '{profile.ApiKeyRef}' did not resolve to a non-empty value.");
        }

        throw new InvalidOperationException($"Profile '{profileId}' must define ApiKeyRef or ApiKey.");
    }

    private static int NormalizePositive(int value, string name)
    {
        if (value < 1)
            throw new InvalidOperationException($"ClientAccess policy '{name}' must be >= 1.");

        return value;
    }
}

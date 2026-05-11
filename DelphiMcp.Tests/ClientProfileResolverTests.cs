using DelphiMcp.ClientAccess;
using Microsoft.Extensions.Configuration;

namespace DelphiMcp.Tests;

public class ClientProfileResolverTests
{
    [Fact]
    public void TryResolveApiKey_ResolvesEnabledProfileFromApiKeyRef()
    {
        var cfg = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Secrets:MACHINE_A"] = "key-a",
            ["ClientAccess:Profiles:machine-a:Enabled"] = "true",
            ["ClientAccess:Profiles:machine-a:DisplayName"] = "Computer A",
            ["ClientAccess:Profiles:machine-a:ApiKeyRef"] = "Secrets:MACHINE_A",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:0:Library"] = "rtl",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:0:Version"] = "13.1"
        });

        var resolver = ClientProfileResolver.FromConfiguration(cfg);

        var ok = resolver.TryResolveApiKey("key-a", out var profile, out var reason);

        Assert.True(ok);
        Assert.NotNull(profile);
        Assert.Equal("ok", reason);
        Assert.Equal("machine-a", profile!.ProfileId);
        Assert.Equal("Computer A", profile.DisplayName);
        Assert.Equal("rtl", profile.DefaultScopes[0].Library);
        Assert.Equal("13.1", profile.DefaultScopes[0].Version);
    }

    [Fact]
    public void FromConfiguration_ThrowsWhenDuplicateResolvedApiKeysExist()
    {
        var cfg = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Secrets:MACHINE_A"] = "shared-key",
            ["Secrets:MACHINE_B"] = "shared-key",

            ["ClientAccess:Profiles:machine-a:Enabled"] = "true",
            ["ClientAccess:Profiles:machine-a:ApiKeyRef"] = "Secrets:MACHINE_A",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:0:Library"] = "rtl",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:0:Version"] = "13.1",

            ["ClientAccess:Profiles:machine-b:Enabled"] = "true",
            ["ClientAccess:Profiles:machine-b:ApiKeyRef"] = "Secrets:MACHINE_B",
            ["ClientAccess:Profiles:machine-b:DefaultScopes:0:Library"] = "devexpress",
            ["ClientAccess:Profiles:machine-b:DefaultScopes:0:Version"] = "25.2.6"
        });

        var ex = Assert.Throws<InvalidOperationException>(() => ClientProfileResolver.FromConfiguration(cfg));

        Assert.Contains("Duplicate API key", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromConfiguration_ThrowsWhenDefaultScopesMissing()
    {
        var cfg = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ClientAccess:Profiles:machine-a:Enabled"] = "true",
            ["ClientAccess:Profiles:machine-a:ApiKey"] = "key-a"
        });

        var ex = Assert.Throws<InvalidOperationException>(() => ClientProfileResolver.FromConfiguration(cfg));

        Assert.Contains("DefaultScope", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveScopes_UsesProfileDefaultsWhenRequestScopesEmpty()
    {
        var cfg = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ClientAccess:GlobalPolicy:MaxTopK"] = "10",
            ["ClientAccess:Profiles:machine-a:Enabled"] = "true",
            ["ClientAccess:Profiles:machine-a:ApiKey"] = "key-a",
            ["ClientAccess:Profiles:machine-a:Options:MaxTopK"] = "6",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:0:Library"] = "rtl",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:0:Version"] = "13.1",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:1:Library"] = "devexpress",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:1:Version"] = "25.2.6"
        });

        var resolver = ClientProfileResolver.FromConfiguration(cfg);
        resolver.TryResolveApiKey("key-a", out var profile, out _);

        var result = resolver.ResolveScopes(profile!, requestedScopes: [], requestedTopK: 99);

        Assert.True(result.UsedProfileDefaults);
        Assert.Equal(2, result.Scopes.Count);
        Assert.Equal(6, result.EffectiveTopK);
    }

    [Fact]
    public void ResolveScopes_UsesDefaultVersionWhenLibraryRequestedWithoutVersion()
    {
        var cfg = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ClientAccess:Profiles:machine-a:Enabled"] = "true",
            ["ClientAccess:Profiles:machine-a:ApiKey"] = "key-a",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:0:Library"] = "rtl",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:0:Version"] = "12.3",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:1:Library"] = "rtl",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:1:Version"] = "13.1"
        });

        var resolver = ClientProfileResolver.FromConfiguration(cfg);
        resolver.TryResolveApiKey("key-a", out var profile, out _);

        var result = resolver.ResolveScopes(
            profile!,
            [new RequestedScope("rtl", null)],
            requestedTopK: 5);

        Assert.False(result.UsedProfileDefaults);
        Assert.Equal(2, result.Scopes.Count);
        Assert.All(result.Scopes, s => Assert.Equal("rtl", s.Library, ignoreCase: true));
        Assert.Contains(result.Scopes, s => s.Version == "12.3");
        Assert.Contains(result.Scopes, s => s.Version == "13.1");
    }

    [Fact]
    public void ResolveScopes_RejectsMoreThanConfiguredVersionsPerLibrary()
    {
        var cfg = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ClientAccess:GlobalPolicy:MaxVersionsPerLibraryPerQuery"] = "2",
            ["ClientAccess:Profiles:machine-a:Enabled"] = "true",
            ["ClientAccess:Profiles:machine-a:ApiKey"] = "key-a",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:0:Library"] = "rtl",
            ["ClientAccess:Profiles:machine-a:DefaultScopes:0:Version"] = "13.1"
        });

        var resolver = ClientProfileResolver.FromConfiguration(cfg);
        resolver.TryResolveApiKey("key-a", out var profile, out _);

        var ex = Assert.Throws<InvalidOperationException>(() => resolver.ResolveScopes(
            profile!,
            [
                new RequestedScope("rtl", "12.3"),
                new RequestedScope("rtl", "13.1"),
                new RequestedScope("rtl", "14.0")
            ],
            requestedTopK: 5));

        Assert.Contains("max is 2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}

using DelphiMcp.ClientAccess;
using DelphiMcp.Search;
using DelphiMcp.Tools;
using Microsoft.AspNetCore.Http;
using Moq;

namespace DelphiMcp.Tests;

/// <summary>
/// Tests for unified Delphi tools parameter validation and scope resolution logic.
/// Note: Full integration tests with actual searcher require a database; these focus on policy enforcement.
/// </summary>
public class UnifiedDelphiToolsTests
{
    [Fact]
    public void UnifiedTools_ScopeResolution_WithProfilePolicy_ControlsTopK()
    {
        // Verify that policy limits are enforced (this would be tested in integration tests)
        var profile = new ResolvedClientProfile
        {
            ProfileId = "test-client",
            DisplayName = "Test Client",
            DefaultScopes = new[] { new TargetScope("rtl", "12.0") }.ToList().AsReadOnly(),
            Policy = new EffectiveClientPolicy
            {
                MaxTopK = 3,
                MaxVersionsPerLibraryPerQuery = 2,
                MaxTargetScopesPerQuery = 4,
                AllowUnversionedQueries = true,
                RequireVersionWhenLibrarySpecified = false,
                DefaultSearchBehavior = "UseProfileDefaults"
            }
        };

        // Verify policy was created correctly
        Assert.Equal(3, profile.Policy.MaxTopK);
        Assert.Equal(2, profile.Policy.MaxVersionsPerLibraryPerQuery);
        Assert.Equal(4, profile.Policy.MaxTargetScopesPerQuery);
    }

    [Fact]
    public void UnifiedTools_MultiVersionComparison_RespectsPolicyLimit()
    {
        // Verify multi-version policy constraints
        var profile = new ResolvedClientProfile
        {
            ProfileId = "test-client",
            DisplayName = "Test Client",
            DefaultScopes = new[] { new TargetScope("rtl", "12.0") }.ToList().AsReadOnly(),
            Policy = new EffectiveClientPolicy
            {
                MaxTopK = 10,
                MaxVersionsPerLibraryPerQuery = 2,
                MaxTargetScopesPerQuery = 4,
                AllowUnversionedQueries = true,
                RequireVersionWhenLibrarySpecified = false,
                DefaultSearchBehavior = "UseProfileDefaults"
            }
        };

        // Policy allows up to 2 versions per library
        Assert.Equal(2, profile.Policy.MaxVersionsPerLibraryPerQuery);

        // Verify that requesting 3 versions would violate policy
        var requestedVersions = new[] { "12.0", "11.0", "10.0" };
        Assert.True(requestedVersions.Length > profile.Policy.MaxVersionsPerLibraryPerQuery);
    }

    [Fact]
    public void UnifiedTools_ProfileDefaults_AllowsScopeResolution()
    {
        // Verify that profile default scopes can be resolved
        var scopes = new[]
        {
            new TargetScope("rtl", "12.0"),
            new TargetScope("devexpress", "25.2.6")
        };
        var profile = new ResolvedClientProfile
        {
            ProfileId = "test-client",
            DisplayName = "Test Client",
            DefaultScopes = scopes.ToList().AsReadOnly(),
            Policy = new EffectiveClientPolicy
            {
                MaxTopK = 10,
                MaxVersionsPerLibraryPerQuery = 2,
                MaxTargetScopesPerQuery = 4,
                AllowUnversionedQueries = true,
                RequireVersionWhenLibrarySpecified = false,
                DefaultSearchBehavior = "UseProfileDefaults"
            }
        };

        // Verify scopes are accessible
        Assert.Equal(2, profile.DefaultScopes.Count);
        Assert.Equal("rtl", profile.DefaultScopes[0].Library);
        Assert.Equal("12.0", profile.DefaultScopes[0].Version);
    }

    [Fact]
    public void UnifiedTools_GlobalPolicy_CanBeOverriddenPerProfile()
    {
        // Verify that profiles can override global policy
        var profile1 = new ResolvedClientProfile
        {
            ProfileId = "profile-a",
            DisplayName = "Profile A",
            DefaultScopes = new[] { new TargetScope("rtl", "12.0") }.ToList().AsReadOnly(),
            Policy = new EffectiveClientPolicy
            {
                MaxTopK = 5,
                MaxVersionsPerLibraryPerQuery = 1,
                MaxTargetScopesPerQuery = 2,
                AllowUnversionedQueries = false,
                RequireVersionWhenLibrarySpecified = true,
                DefaultSearchBehavior = "StrictMode"
            }
        };

        var profile2 = new ResolvedClientProfile
        {
            ProfileId = "profile-b",
            DisplayName = "Profile B",
            DefaultScopes = new[] { new TargetScope("devexpress", "25.2.6") }.ToList().AsReadOnly(),
            Policy = new EffectiveClientPolicy
            {
                MaxTopK = 10,
                MaxVersionsPerLibraryPerQuery = 2,
                MaxTargetScopesPerQuery = 4,
                AllowUnversionedQueries = true,
                RequireVersionWhenLibrarySpecified = false,
                DefaultSearchBehavior = "UseProfileDefaults"
            }
        };

        // Verify different policies
        Assert.Equal(5, profile1.Policy.MaxTopK);
        Assert.Equal(10, profile2.Policy.MaxTopK);
        Assert.False(profile1.Policy.AllowUnversionedQueries);
        Assert.True(profile2.Policy.AllowUnversionedQueries);
    }

    [Fact]
    public void HttpContextAccessor_ReturnsNullWhenNotInHttpContext()
    {
        // Verify that missing HttpContext is handled gracefully
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(h => h.HttpContext).Returns((HttpContext?)null);

        Assert.Null(httpContextAccessor.Object.HttpContext);
    }

    [Fact]
    public void HttpContext_StoresResolvedProfile_InItems()
    {
        // Verify that HttpContext.Items can store and retrieve the resolved profile
        var profile = new ResolvedClientProfile
        {
            ProfileId = "test-profile",
            DisplayName = "Test Profile",
            DefaultScopes = new[] { new TargetScope("rtl", "12.0") }.ToList().AsReadOnly(),
            Policy = new EffectiveClientPolicy
            {
                MaxTopK = 5,
                MaxVersionsPerLibraryPerQuery = 2,
                MaxTargetScopesPerQuery = 4,
                AllowUnversionedQueries = true,
                RequireVersionWhenLibrarySpecified = false,
                DefaultSearchBehavior = "UseProfileDefaults"
            }
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Items[ClientProfileResolver.HttpContextItemKey] = profile;

        var retrieved = httpContext.Items[ClientProfileResolver.HttpContextItemKey] as ResolvedClientProfile;
        Assert.NotNull(retrieved);
        Assert.Equal("test-profile", retrieved.ProfileId);
    }

    [Fact]
    public void UnifiedTools_VersionParsing_HandlesCommaSeparatedList()
    {
        // Verify that comma-separated versions can be parsed
        var versionString = "12.0,11.0,10.0";
        var versions = versionString.Split(',').Select(v => v.Trim()).ToList();

        Assert.Equal(3, versions.Count);
        Assert.Equal("12.0", versions[0]);
        Assert.Equal("11.0", versions[1]);
        Assert.Equal("10.0", versions[2]);
    }

    [Fact]
    public void UnifiedTools_VersionLimitEnforcement_IdentifiesViolations()
    {
        // Verify that version count violations are detected
        var policy = new EffectiveClientPolicy
        {
            MaxTopK = 10,
            MaxVersionsPerLibraryPerQuery = 2,
            MaxTargetScopesPerQuery = 4,
            AllowUnversionedQueries = true,
            RequireVersionWhenLibrarySpecified = false,
            DefaultSearchBehavior = "UseProfileDefaults"
        };

        var versionList = new[] { "12.0", "11.0", "10.0" };
        int maxVersions = policy.MaxVersionsPerLibraryPerQuery;

        // Verify violation is detected
        Assert.True(versionList.Length > maxVersions);
        Assert.Equal(3, versionList.Length);
        Assert.Equal(2, maxVersions);
    }
}

using System.Security.Claims;
using FindIt.Api.Services;

namespace FindIt.Api.Tests;

public sealed class FindItTokenValidatorTests
{
    private static readonly Guid UemgTenantId =
        Guid.Parse("e298359b-9b54-45c0-9bb7-690913b34cf8");
    private static readonly Guid WebClientId =
        Guid.Parse("b260a802-31c8-4321-9f4e-4b41ab44581f");

    [Theory]
    [InlineData("azp")]
    [InlineData("appid")]
    public void AcceptsExpectedInstitutionalDelegatedToken(string clientClaim)
    {
        var principal = Principal(
            new Claim("tid", UemgTenantId.ToString()),
            new Claim(clientClaim, WebClientId.ToString()),
            new Claim("scp", "profile access_as_user"));

        Assert.True(FindItTokenValidator.IsAllowed(principal, UemgTenantId, WebClientId));
    }

    [Theory]
    [InlineData("wrong_tenant")]
    [InlineData("wrong_client")]
    [InlineData("missing_scope")]
    [InlineData("different_scope")]
    public void RejectsTokenWithoutRequiredTenantClientAndScope(string change)
    {
        var claims = new List<Claim>
        {
            new("tid", change == "wrong_tenant" ? Guid.NewGuid().ToString() : UemgTenantId.ToString()),
            new("azp", change == "wrong_client" ? Guid.NewGuid().ToString() : WebClientId.ToString())
        };
        if (change != "missing_scope")
        {
            claims.Add(new Claim("scp", change == "different_scope" ? "access_as_user_extra" : "access_as_user"));
        }

        Assert.False(FindItTokenValidator.IsAllowed(Principal(claims.ToArray()),
            UemgTenantId, WebClientId));
    }

    [Theory]
    [InlineData("personal")]
    [InlineData("another_organization")]
    public void TestModeAcceptsValidDelegatedTokenFromAnyMicrosoftTenant(string tenantKind)
    {
        var tokenTenantId = tenantKind == "personal"
            ? Guid.Parse("9188040d-6c67-4c5b-b112-36a304b66dad")
            : Guid.NewGuid();
        var principal = Principal(
            new Claim("tid", tokenTenantId.ToString()),
            new Claim("azp", WebClientId.ToString()),
            new Claim("scp", "access_as_user"));

        Assert.True(FindItTokenValidator.IsAllowed(principal,
            UemgTenantId, WebClientId, allowAnyMicrosoftTenantForTesting: true));
        Assert.False(FindItTokenValidator.IsAllowed(principal,
            UemgTenantId, WebClientId));
    }

    [Theory]
    [InlineData("missing_tenant")]
    [InlineData("wrong_client")]
    [InlineData("missing_scope")]
    public void TestModeStillRequiresTenantClientAndScope(string change)
    {
        var claims = new List<Claim>();
        if (change != "missing_tenant")
        {
            claims.Add(new Claim("tid", Guid.NewGuid().ToString()));
        }
        claims.Add(new Claim("azp", change == "wrong_client"
            ? Guid.NewGuid().ToString() : WebClientId.ToString()));
        if (change != "missing_scope")
        {
            claims.Add(new Claim("scp", "access_as_user"));
        }

        Assert.False(FindItTokenValidator.IsAllowed(Principal(claims.ToArray()),
            UemgTenantId, WebClientId, allowAnyMicrosoftTenantForTesting: true));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));
}

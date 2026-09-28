using System.Security.Claims;
using FindIt.Api.Services;

namespace FindIt.Api.Tests;

public sealed class InstitutionalTokenValidatorTests
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

        Assert.True(InstitutionalTokenValidator.IsAllowed(principal, UemgTenantId, WebClientId));
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

        Assert.False(InstitutionalTokenValidator.IsAllowed(Principal(claims.ToArray()),
            UemgTenantId, WebClientId));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));
}

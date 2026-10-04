using System.Security.Claims;

namespace FindIt.Api.Services;

public static class FindItTokenValidator
{
    public static bool IsAllowed(
        ClaimsPrincipal principal, Guid institutionalTenantId, Guid webClientId,
        bool allowAnyMicrosoftTenantForTesting = false)
    {
        if (principal.Identity?.IsAuthenticated != true
            || institutionalTenantId == Guid.Empty || webClientId == Guid.Empty
            || !Guid.TryParse(FindClaim(principal, "tid",
                "http://schemas.microsoft.com/identity/claims/tenantid"), out var tokenTenantId)
            || tokenTenantId == Guid.Empty
            || (!allowAnyMicrosoftTenantForTesting && tokenTenantId != institutionalTenantId)
            || !Guid.TryParse(FindClaim(principal, "azp", "appid",
                "http://schemas.microsoft.com/identity/claims/appid"), out var tokenClientId)
            || tokenClientId != webClientId)
        {
            return false;
        }

        var scopes = FindClaim(principal, "scp",
            "http://schemas.microsoft.com/identity/claims/scope");
        return scopes?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains("access_as_user", StringComparer.Ordinal) == true;
    }

    private static string? FindClaim(ClaimsPrincipal principal, params string[] names)
    {
        foreach (var name in names)
        {
            var value = principal.FindFirstValue(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}

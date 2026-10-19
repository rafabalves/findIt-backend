using System.Security.Claims;
using FindIt.Api.Data;
using FindIt.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Services;

public sealed class CurrentUserService(
    FindItDbContext db, TimeProvider clock, IConfiguration configuration, IHostEnvironment environment)
{
    private static readonly Guid UemgTenantId =
        Guid.Parse("e298359b-9b54-45c0-9bb7-690913b34cf8");

    public async Task<User> GetOrCreateAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("Autenticação Microsoft necessária.");
        }

        var tenantId = ParseClaim(principal, "tid",
            "http://schemas.microsoft.com/identity/claims/tenantid");
        var objectId = ParseClaim(principal, "oid",
            "http://schemas.microsoft.com/identity/claims/objectidentifier");
        var email = new[]
        {
            principal.FindFirstValue("preferred_username"),
            principal.FindFirstValue("email"),
            principal.FindFirstValue(ClaimTypes.Email),
            principal.FindFirstValue("upn"),
            principal.FindFirstValue(ClaimTypes.Upn),
            principal.FindFirstValue("unique_name")
        }.FirstOrDefault(value => value is not null && IsAllowedEmail(value.Trim(), tenantId))?.Trim();
        if (email is null || email.Length > 255)
        {
            throw new UnauthorizedAccessException("A conta não é permitida neste perfil de autenticação.");
        }

        var name = principal.FindFirstValue("name")
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? email;
        name = name.Trim();
        if (name.Length > 255)
        {
            name = name[..255];
        }

        var user = await db.Users.SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.ObjectId == objectId, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                TenantId = tenantId,
                ObjectId = objectId,
                Name = name,
                Email = email,
                CreatedAt = clock.GetUtcNow().UtcDateTime
            };
            db.Users.Add(user);
        }
        else
        {
            user.Name = name;
            user.Email = email;
        }

        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static Guid ParseClaim(ClaimsPrincipal principal, params string[] names)
    {
        foreach (var name in names)
        {
            if (Guid.TryParse(principal.FindFirstValue(name), out var value)
                && value != Guid.Empty)
            {
                return value;
            }
        }
        throw new UnauthorizedAccessException("O token não contém o identificador de conta esperado.");
    }

    private static bool IsInstitutionalEmail(string email) =>
        email.EndsWith("@discente.uemg.br", StringComparison.OrdinalIgnoreCase)
        || email.EndsWith("@docente.uemg.br", StringComparison.OrdinalIgnoreCase);

    private bool IsAllowedEmail(string email, Guid tenantId) =>
        (tenantId == UemgTenantId && IsInstitutionalEmail(email))
        || (FindItAuthMode.AllowsAnyMicrosoftAccountForTesting(configuration, environment)
            && email.Contains('@'));
}

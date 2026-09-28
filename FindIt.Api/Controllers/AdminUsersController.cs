using FindIt.Api.Contracts;
using FindIt.Api.Data;
using FindIt.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/users")]
public sealed class AdminUsersController(
    CurrentUserService currentUser, AdministrationService administration,
    FindItDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<AdminUserDto>> List(
        [FromQuery] string? search, CancellationToken cancellationToken)
    {
        await RequireAdminAsync(cancellationToken);
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.Email.Contains(term));
        }
        return await query.OrderBy(x => x.Name).Take(100)
            .Select(x => new AdminUserDto(x.Id, x.Name, x.Email, x.IsAdmin))
            .ToListAsync(cancellationToken);
    }

    [HttpPatch("{id:int}/admin")]
    public async Task<IActionResult> SetAdministrator(
        int id, SetAdministratorDto input, CancellationToken cancellationToken)
    {
        var administrator = await RequireAdminAsync(cancellationToken);
        await administration.SetAdministratorAsync(id, administrator, input.IsAdmin, cancellationToken);
        return NoContent();
    }

    private async Task<int> RequireAdminAsync(CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(User, cancellationToken);
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException("Esta ação exige permissão de administrador.");
        }
        return user.Id;
    }
}

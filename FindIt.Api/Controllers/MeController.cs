using FindIt.Api.Contracts;
using FindIt.Api.Data;
using FindIt.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(CurrentUserService currentUser, FindItDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<MeDto> Get(CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(User, cancellationToken);
        return new MeDto(user.Id, user.Name, user.Email, user.IsAdmin);
    }

    [HttpGet("requests")]
    public async Task<List<RequestDto>> Requests(CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(User, cancellationToken);
        return await db.RecoveryRequests.AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new RequestDto(x.Id, x.ItemId, x.Item.Name, x.Status,
                x.Justification, x.AdminNotes, x.ExpiresAt, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }
}

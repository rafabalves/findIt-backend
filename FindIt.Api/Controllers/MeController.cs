using FindIt.Api.Contracts;
using FindIt.Api.Data;
using FindIt.Api.Domain;
using FindIt.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public sealed class MeController(
    CurrentUserService currentUser, FindItDbContext db,
    RecoveryWorkflowService workflow, TimeProvider clock) : ControllerBase
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
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.RecoveryRequests.AsNoTracking()
            .Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new RequestDto(x.Id, x.ItemId, x.Item.Name, x.Status,
                x.Justification, x.AdminNotes, x.ExpiresAt, x.CreatedAt, x.UpdatedAt,
                x.Status == RequestStatus.Approved && x.ExpiresAt > now
                    && x.Item.Status == ItemStatus.Active))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("requests/{id:int}/pickup-code")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<PickupCodeDto> PickupCode(int id, CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(User, cancellationToken);
        return new PickupCodeDto(await workflow.GetPickupCodeAsync(id, user.Id, cancellationToken));
    }
}

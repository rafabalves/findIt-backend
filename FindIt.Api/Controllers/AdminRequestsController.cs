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
[Route("api/admin/requests")]
public sealed class AdminRequestsController(
    CurrentUserService currentUser, RecoveryWorkflowService workflow,
    FindItDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<object>>> List(
        [FromQuery] RequestStatus? status, CancellationToken cancellationToken)
    {
        await RequireAdminAsync(cancellationToken);
        var query = db.RecoveryRequests.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }
        var requests = await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id, x.ItemId, ItemName = x.Item.Name,
                x.UserId, UserName = x.User.Name, UserEmail = x.User.Email,
                x.Status, x.Justification, x.AdminNotes, x.ExpiresAt,
                x.CreatedAt, x.UpdatedAt
            }).Take(100).ToListAsync(cancellationToken);
        return Ok(requests);
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(
        int id, ApproveRequestDto input, CancellationToken cancellationToken)
    {
        var administrator = await RequireAdminAsync(cancellationToken);
        await workflow.ApproveAsync(id, administrator.Id, input.AdminNotes, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(
        int id, RejectRequestDto input, CancellationToken cancellationToken)
    {
        var administrator = await RequireAdminAsync(cancellationToken);
        await workflow.RejectAsync(id, administrator.Id, input.AdminNotes, cancellationToken);
        return NoContent();
    }

    private async Task<User> RequireAdminAsync(CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(User, cancellationToken);
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException("Esta ação exige permissão de administrador.");
        }
        return user;
    }
}

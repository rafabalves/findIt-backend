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
[Route("api/admin/items")]
public sealed class AdminItemsController(
    CurrentUserService currentUser, ItemManagementService items,
    RecoveryWorkflowService workflow, ItemPhotoService photos, FindItDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(
        CreateItemDto input, CancellationToken cancellationToken)
    {
        var administrator = await RequireAdminAsync(cancellationToken);
        var id = await items.CreateAsync(administrator.Id,
            new NewItem(input.CategoryId, input.QRCodeGuid, input.Name,
                input.Description, input.Location, input.FoundOn), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id, UpdateItemDto input, CancellationToken cancellationToken)
    {
        var administrator = await RequireAdminAsync(cancellationToken);
        await items.UpdateAsync(id, administrator.Id,
            new ItemChanges(input.CategoryId, input.Name, input.Description,
                input.Location, input.FoundOn), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:int}/photos")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(16_000_000)]
    public async Task<ActionResult<IReadOnlyList<ItemPhotoDto>>> ReplacePhotos(
        int id, [FromForm] List<IFormFile>? files, CancellationToken cancellationToken)
    {
        var administrator = await RequireAdminAsync(cancellationToken);
        return Ok(await photos.ReplaceAsync(id, administrator.Id, files ?? [], cancellationToken));
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Patch(
        int id, PatchItemDto input, CancellationToken cancellationToken)
    {
        var administrator = await RequireAdminAsync(cancellationToken);
        await items.PatchAsync(id, administrator.Id,
            new ItemPatch(input.CategoryId, input.Name, input.Description,
                input.Location, input.FoundOn), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/return")]
    public async Task<IActionResult> Return(int id, CancellationToken cancellationToken)
    {
        var administrator = await RequireAdminAsync(cancellationToken);
        await workflow.ReturnAsync(id, administrator.Id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(
        int id, ChangeItemStatusDto input, CancellationToken cancellationToken)
    {
        var administrator = await RequireAdminAsync(cancellationToken);
        await workflow.OverrideStatusAsync(id, administrator.Id, input.Status,
            input.Reason, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/activity")]
    public async Task<ActionResult<List<ActivityDto>>> Activity(
        int id, CancellationToken cancellationToken)
    {
        await RequireAdminAsync(cancellationToken);
        if (!await db.Items.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return NotFound();
        }
        return await db.ActivityLogs.AsNoTracking().Where(x => x.ItemId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ActivityDto(x.Id, x.UserId, x.Type,
                x.Description, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("{id:int}/qr-code")]
    public async Task<ActionResult<QRCodeDto>> QrCode(
        int id, CancellationToken cancellationToken)
    {
        await RequireAdminAsync(cancellationToken);
        var code = await db.Items.AsNoTracking()
            .Where(x => x.Id == id && x.QRCode != null)
            .Select(x => new QRCodeDto(x.QRCode!.Id, x.QRCode.Guid))
            .SingleOrDefaultAsync(cancellationToken);
        return code is null ? NotFound() : Ok(code);
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

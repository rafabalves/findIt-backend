using FindIt.Api.Contracts;
using FindIt.Api.Data;
using FindIt.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/qr-codes")]
public sealed class AdminQRCodesController(
    CurrentUserService currentUser, ItemManagementService items,
    FindItDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<List<QRCodeDto>>> Generate(
        GenerateQRCodesDto input, CancellationToken cancellationToken)
    {
        var user = await RequireAdminAsync(cancellationToken);
        var codes = await items.GenerateQRCodesAsync(user.Id, input.Count, cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            codes.Select(x => new QRCodeDto(x.Id, x.Guid, x.Code)).ToList());
    }

    [HttpGet("available")]
    public async Task<List<QRCodeDto>> Available(CancellationToken cancellationToken)
    {
        await RequireAdminAsync(cancellationToken);
        return await db.QRCodes.AsNoTracking().Where(x => x.Item == null)
            .OrderBy(x => x.Id)
            .Select(x => new QRCodeDto(x.Id, x.Guid, x.Code))
            .ToListAsync(cancellationToken);
    }

    private async Task<FindIt.Api.Domain.User> RequireAdminAsync(CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(User, cancellationToken);
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException("Esta ação exige permissão de administrador.");
        }
        return user;
    }
}

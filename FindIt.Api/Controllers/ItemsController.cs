using FindIt.Api.Contracts;
using FindIt.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindIt.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/items")]
public sealed class ItemsController(ItemQueryService items) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ItemPublicDto>> Search(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        items.SearchActiveAsync(search, categoryId, page, pageSize, cancellationToken);

    [HttpGet("recently-closed")]
    public Task<PagedResult<ItemPublicDto>> RecentlyClosed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        items.RecentlyClosedAsync(page, pageSize, cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ItemPublicDto>> ById(
        int id, CancellationToken cancellationToken)
    {
        var item = await items.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("by-qr/{guid:guid}")]
    public async Task<ActionResult<ItemPublicDto>> ByQr(
        Guid guid, CancellationToken cancellationToken)
    {
        var item = await items.GetByQrAsync(guid, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}

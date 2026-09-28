using FindIt.Api.Contracts;
using FindIt.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/categories")]
public sealed class CategoriesController(FindItDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<CategoryPublicDto>> List(CancellationToken cancellationToken) =>
        db.Categories.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new CategoryPublicDto(x.Id, x.Name))
            .ToListAsync(cancellationToken);
}

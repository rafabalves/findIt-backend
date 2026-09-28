using FindIt.Api.Contracts;
using FindIt.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindIt.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/categories")]
public sealed class AdminCategoriesController(
    CurrentUserService currentUser, AdministrationService administration) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(
        CategoryInputDto input, CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(User, cancellationToken);
        var id = await administration.CreateCategoryAsync(user.Id, input.Name, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Rename(
        int id, CategoryInputDto input, CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(User, cancellationToken);
        await administration.RenameCategoryAsync(id, user.Id, input.Name, cancellationToken);
        return NoContent();
    }
}

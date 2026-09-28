using FindIt.Api.Contracts;
using FindIt.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FindIt.Api.Controllers;

[ApiController]
[Authorize]
public sealed class RecoveryRequestsController(
    CurrentUserService currentUser, RecoveryWorkflowService workflow) : ControllerBase
{
    [HttpPost("api/items/{itemId:int}/requests")]
    public async Task<ActionResult<object>> Create(
        int itemId, CreateRequestDto input, CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(User, cancellationToken);
        var id = await workflow.CreateAsync(itemId, user.Id, input.Justification, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { id });
    }

    [HttpPost("api/requests/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var user = await currentUser.GetOrCreateAsync(User, cancellationToken);
        await workflow.CancelAsync(id, user.Id, cancellationToken);
        return NoContent();
    }
}

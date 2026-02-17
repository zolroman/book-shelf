using Bookshelf.Shared.Contracts.Auth;
using Bookshelf.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IBookshelfRepository repository) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> GetCurrentUser([FromQuery] int userId = 1, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetUserAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new UserProfileDto(user.Id, user.Login, user.DisplayName));
    }
}

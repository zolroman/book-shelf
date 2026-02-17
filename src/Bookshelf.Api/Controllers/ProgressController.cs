using Bookshelf.Api.Mappers;
using Bookshelf.Api.Parsing;
using Bookshelf.Infrastructure.Services;
using Bookshelf.Shared.Contracts.Progress;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProgressController(IBookshelfRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProgressSnapshotDto>> GetSnapshot(
        [FromQuery] int userId,
        [FromQuery] int bookId,
        [FromQuery] string formatType,
        CancellationToken cancellationToken = default)
    {
        if (!RequestParser.TryParseBookFormatType(formatType, out var parsedFormat))
        {
            return BadRequest("Unknown format type.");
        }

        var snapshot = await repository.GetProgressSnapshotAsync(userId, bookId, parsedFormat, cancellationToken);
        if (snapshot is null)
        {
            return NotFound();
        }

        return Ok(snapshot.ToDto());
    }

    [HttpPut]
    public async Task<ActionResult<ProgressSnapshotDto>> Upsert(
        [FromBody] UpsertProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!RequestParser.TryParseBookFormatType(request.FormatType, out var parsedFormat))
        {
            return BadRequest("Unknown format type.");
        }

        var snapshot = await repository.UpsertProgressSnapshotAsync(
            request.UserId,
            request.BookId,
            parsedFormat,
            request.PositionRef,
            request.ProgressPercent,
            cancellationToken);

        return Ok(snapshot.ToDto());
    }
}

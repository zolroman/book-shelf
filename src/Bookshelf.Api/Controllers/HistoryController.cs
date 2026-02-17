using Bookshelf.Api.Mappers;
using Bookshelf.Api.Parsing;
using Bookshelf.Infrastructure.Services;
using Bookshelf.Shared.Contracts.History;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HistoryController(IBookshelfRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HistoryEventDto>>> GetHistory(
        [FromQuery] int userId = 1,
        [FromQuery] int? bookId = null,
        CancellationToken cancellationToken = default)
    {
        var history = await repository.GetHistoryEventsAsync(userId, bookId, cancellationToken);
        return Ok(history.Select(x => x.ToDto()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<HistoryEventDto>> Add(
        [FromBody] AddHistoryEventRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!RequestParser.TryParseBookFormatType(request.FormatType, out var parsedFormat))
        {
            return BadRequest("Unknown format type.");
        }

        if (!RequestParser.TryParseHistoryEventType(request.EventType, out var parsedEventType))
        {
            return BadRequest("Unknown event type.");
        }

        var historyEvent = await repository.AddHistoryEventAsync(
            request.UserId,
            request.BookId,
            parsedFormat,
            parsedEventType,
            request.PositionRef,
            request.EventAtUtc ?? DateTime.UtcNow,
            cancellationToken);

        return Ok(historyEvent.ToDto());
    }
}

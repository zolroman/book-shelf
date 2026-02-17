using Bookshelf.Api.Mappers;
using Bookshelf.Infrastructure.Services;
using Bookshelf.Shared.Contracts.Library;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LibraryController(IBookshelfRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LibraryBookDto>>> GetLibrary([FromQuery] int userId = 1, CancellationToken cancellationToken = default)
    {
        var libraryItems = await repository.GetLibraryItemsAsync(userId, cancellationToken);
        var result = new List<LibraryBookDto>(libraryItems.Count);

        foreach (var item in libraryItems)
        {
            var book = await repository.GetBookAsync(item.BookId, cancellationToken);
            if (book is null)
            {
                continue;
            }

            var authors = await repository.GetAuthorsForBookAsync(item.BookId, cancellationToken);
            var formats = await repository.GetFormatsForBookAsync(item.BookId, cancellationToken);

            result.Add(new LibraryBookDto(item.ToDto(), book.ToSummaryDto(authors, formats)));
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<LibraryItemDto>> AddToLibrary([FromBody] AddLibraryItemRequest request, CancellationToken cancellationToken = default)
    {
        var added = await repository.AddLibraryItemAsync(request.UserId, request.BookId, cancellationToken);
        return Ok(added.ToDto());
    }

    [HttpDelete("{bookId:int}")]
    public async Task<IActionResult> RemoveFromLibrary(
        int bookId,
        [FromQuery] int userId = 1,
        CancellationToken cancellationToken = default)
    {
        var removed = await repository.RemoveLibraryItemAsync(userId, bookId, cancellationToken);
        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("{bookId:int}/rating")]
    public async Task<ActionResult<LibraryItemDto>> RateBook(
        int bookId,
        [FromQuery] int userId,
        [FromQuery] float rating,
        CancellationToken cancellationToken = default)
    {
        var item = await repository.GetLibraryItemAsync(userId, bookId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.SetRating(rating);
        return Ok(item.ToDto());
    }
}

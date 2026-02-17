using Bookshelf.Api.Mappers;
using Bookshelf.Infrastructure.Services;
using Bookshelf.Shared.Contracts.Books;
using Bookshelf.Shared.Contracts.Search;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SearchController(
    IBookSearchProvider searchProvider,
    IBookshelfRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SearchResultDto>> Search(
        [FromQuery] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query is required.");
        }

        var books = await searchProvider.SearchAsync(query, cancellationToken);
        var items = new List<BookSummaryDto>(books.Count);

        foreach (var book in books)
        {
            var authors = await repository.GetAuthorsForBookAsync(book.Id, cancellationToken);
            var formats = await repository.GetFormatsForBookAsync(book.Id, cancellationToken);
            items.Add(book.ToSummaryDto(authors, formats));
        }

        return Ok(new SearchResultDto(query, items));
    }
}

using Bookshelf.Api.Mappers;
using Bookshelf.Infrastructure.Services;
using Bookshelf.Shared.Contracts.Books;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BooksController(IBookshelfRepository repository) : ControllerBase
{
    private readonly IBookshelfRepository _repository = repository;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookSummaryDto>>> GetBooks(
        [FromQuery] string? query,
        [FromQuery] string? author,
        CancellationToken cancellationToken = default)
    {
        var books = await _repository.GetBooksAsync(query, author, cancellationToken);
        var result = new List<BookSummaryDto>(books.Count);

        foreach (var book in books)
        {
            var authors = await _repository.GetAuthorsForBookAsync(book.Id, cancellationToken);
            var formats = await _repository.GetFormatsForBookAsync(book.Id, cancellationToken);
            result.Add(book.ToSummaryDto(authors, formats));
        }

        return Ok(result);
    }

    [HttpGet("{bookId:int}")]
    public async Task<ActionResult<BookDetailsDto>> GetById(int bookId, CancellationToken cancellationToken = default)
    {
        var book = await _repository.GetBookAsync(bookId, cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        var authors = await _repository.GetAuthorsForBookAsync(book.Id, cancellationToken);
        var formats = await _repository.GetFormatsForBookAsync(book.Id, cancellationToken);

        return Ok(book.ToDetailsDto(authors, formats));
    }
}

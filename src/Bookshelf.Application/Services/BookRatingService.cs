using Bookshelf.Application.Abstractions.Persistence;
using Bookshelf.Application.Abstractions.Services;
using Bookshelf.Application.Exceptions;
using Bookshelf.Domain.Entities;
using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Services;

public sealed class BookRatingService : IBookRatingService
{
    private readonly IBookRatingRepository _bookRatingRepository;
    private readonly IBookRepository _bookRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BookRatingService(
        IBookRatingRepository bookRatingRepository,
        IBookRepository bookRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _bookRatingRepository = bookRatingRepository;
        _bookRepository = bookRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<int?> GetRatingAsync(
        long userId,
        long bookId,
        CancellationToken cancellationToken = default)
    {
        if (bookId <= 0)
        {
            throw new ArgumentException("bookId must be greater than zero.", nameof(bookId));
        }

        var existing = await _bookRatingRepository.GetAsync(userId, bookId, cancellationToken);
        return existing?.Rating;
    }

    public async Task<BookUserRatingDto> UpsertAsync(
        long userId,
        long bookId,
        int rating,
        CancellationToken cancellationToken = default)
    {
        if (bookId <= 0)
        {
            throw new ArgumentException("bookId must be greater than zero.", nameof(bookId));
        }

        if (rating is < 1 or > 5)
        {
            throw new ArgumentException("rating must be between 1 and 5.", nameof(rating));
        }

        var existingBook = await _bookRepository.GetByIdAsync(bookId, cancellationToken);
        if (existingBook is null)
        {
            throw new BookIdNotFoundException(bookId);
        }

        await _userRepository.EnsureExistsAsync(userId, cancellationToken);

        var existing = await _bookRatingRepository.GetAsync(userId, bookId, cancellationToken);
        if (existing is null)
        {
            var created = new BookRating(userId, bookId, rating);
            await _bookRatingRepository.AddAsync(created, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new BookUserRatingDto(created.UserId, created.BookId, created.Rating, created.UpdatedAtUtc);
        }

        existing.Update(rating);
        _bookRatingRepository.Update(existing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new BookUserRatingDto(existing.UserId, existing.BookId, existing.Rating, existing.UpdatedAtUtc);
    }

    public async Task DeleteAsync(
        long userId,
        long bookId,
        CancellationToken cancellationToken = default)
    {
        if (bookId <= 0)
        {
            throw new ArgumentException("bookId must be greater than zero.", nameof(bookId));
        }

        var existingBook = await _bookRepository.GetByIdAsync(bookId, cancellationToken);
        if (existingBook is null)
        {
            throw new BookIdNotFoundException(bookId);
        }

        var existing = await _bookRatingRepository.GetAsync(userId, bookId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        _bookRatingRepository.Remove(existing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

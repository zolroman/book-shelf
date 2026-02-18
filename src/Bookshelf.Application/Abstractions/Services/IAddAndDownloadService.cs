using Bookshelf.Shared.Contracts.Api;

namespace Bookshelf.Application.Abstractions.Services;

public interface IAddAndDownloadService
{
    Task<AddAndDownloadResponse> ExecuteAsync(
        AddAndDownloadRequest request,
        CancellationToken cancellationToken = default);
}

using System.Net;
using Bookshelf.Api.Api.Errors;

namespace Bookshelf.Api.Api.Endpoints.Common;

internal static class EndpointGuards
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    internal static string EnsureMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ApiException(
                ApiErrorCodes.MediaTypeRequired,
                "mediaType is required.",
                HttpStatusCode.BadRequest);
        }

        var normalized = mediaType.Trim().ToLowerInvariant();
        if (normalized is not ("text" or "audio"))
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "mediaType must be either text or audio.",
                HttpStatusCode.BadRequest);
        }

        return normalized;
    }

    internal static void EnsureRequired(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                $"{argumentName} is required.",
                HttpStatusCode.BadRequest);
        }
    }

    internal static int NormalizePage(int? page)
    {
        return page is null or < 1 ? DefaultPage : page.Value;
    }

    internal static PaginationQuery NormalizePaging(int? page, int? pageSize)
    {
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = pageSize is null or < 1 or > MaxPageSize ? DefaultPageSize : pageSize.Value;
        return new PaginationQuery(normalizedPage, normalizedPageSize);
    }
}

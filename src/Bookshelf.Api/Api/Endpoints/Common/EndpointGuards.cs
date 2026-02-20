using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Errors;

namespace Bookshelf.Api.Api.Endpoints.Common;

internal static class EndpointGuards
{
    internal static long EnsureUserId(long? userId)
    {
        if (!userId.HasValue || userId.Value <= 0)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "userId must be greater than zero.",
                HttpStatusCode.BadRequest);
        }

        return userId.Value;
    }

    internal static long EnsureUserIdFromClaims(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("userId")
            ?? user.FindFirstValue("sub");

        if (long.TryParse(raw, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        throw new ApiException(
            ApiErrorCodes.InvalidArgument,
            "Authenticated user id claim is missing or invalid.",
            HttpStatusCode.BadRequest);
    }

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
}

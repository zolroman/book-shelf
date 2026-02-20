using System.Net;
using Bookshelf.Api.Api.Endpoints.Common;
using Bookshelf.Api.Api.Errors;
using Bookshelf.Application.Abstractions.Services;

namespace Bookshelf.Api.Api.Endpoints.Library;

public static class GetLibraryEndpoint
{
    public static RouteGroupBuilder MapGetLibraryEndpoint(this RouteGroupBuilder v1)
    {
        v1.MapGet("library", Handle);
        return v1;
    }

    private static async Task<IResult> Handle(
        bool? includeArchived,
        string? query,
        string? providerCode,
        string? catalogState,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        ILibraryService libraryService,
        CancellationToken cancellationToken)
    {
        var userId = EndpointGuards.EnsureUserIdFromClaims(httpContext.User);
        var safePage = !page.HasValue || page.Value < 1 ? 1 : page.Value;
        var safePageSize = !pageSize.HasValue || pageSize.Value is < 1 or > 100 ? 20 : pageSize.Value;
        var includeArchivedValue = includeArchived ?? false;

        try
        {
            var response = await libraryService.ListAsync(
                userId,
                includeArchivedValue,
                query,
                providerCode,
                catalogState,
                safePage,
                safePageSize,
                cancellationToken);

            return Results.Ok(response);
        }
        catch (ArgumentException)
        {
            throw new ApiException(
                ApiErrorCodes.InvalidArgument,
                "Invalid library filter argument.",
                HttpStatusCode.BadRequest);
        }
    }
}

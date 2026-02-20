using System.Net;
using System.Security.Claims;
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
        ClaimsPrincipal user,
        ILibraryService libraryService,
        CancellationToken cancellationToken)
    {
        var userId = user.Id;
        var pagination = EndpointGuards.NormalizePaging(page, pageSize);
        var includeArchivedValue = includeArchived ?? false;

        try
        {
            var response = await libraryService.ListAsync(
                userId,
                includeArchivedValue,
                query,
                providerCode,
                catalogState,
                pagination.Page,
                pagination.PageSize,
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

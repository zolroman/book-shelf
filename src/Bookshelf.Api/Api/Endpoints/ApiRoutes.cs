using Bookshelf.Api.Api.Endpoints.DownloadJobs;
using Bookshelf.Api.Api.Endpoints.History;
using Bookshelf.Api.Api.Endpoints.Library;
using Bookshelf.Api.Api.Endpoints.Progress;
using Bookshelf.Api.Api.Endpoints.SearchBooks;
using Bookshelf.Api.Api.Endpoints.Shelves;

namespace Bookshelf.Api.Api.Endpoints;

public static class ApiRoutes
{
    public static IEndpointRouteBuilder MapV1Endpoints(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1")
            .RequireRateLimiting("api-v1")
            .RequireAuthorization();
        v1.MapSearchBooksEndpoint();
        v1.MapSearchBookDetailsEndpoint();
        v1.MapSearchBookCandidatesEndpoint();
        v1.MapGetLibraryEndpoint();
        v1.MapAddAndDownloadEndpoint();
        v1.MapUpsertProgressEndpoint();
        v1.MapListProgressEndpoint();
        v1.MapAppendHistoryEventsEndpoint();
        v1.MapListHistoryEventsEndpoint();
        v1.MapListDownloadJobsEndpoint();
        v1.MapGetDownloadJobEndpoint();
        v1.MapCancelDownloadJobEndpoint();
        v1.MapGetShelvesEndpoint();
        v1.MapCreateShelfEndpoint();
        v1.MapAddBookToShelfEndpoint();
        v1.MapRemoveBookFromShelfEndpoint();

        return app;
    }
}

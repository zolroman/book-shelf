using Bookshelf.Shared.Client;
using Bookshelf.Offline;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace Bookshelf;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<UserSessionState>();

        var configuredBaseUrl = builder.Configuration["BookshelfApi:BaseUrl"];
        var envBaseUrl = Environment.GetEnvironmentVariable("BOOKSHELF_API_BASE_URL");
        var apiBaseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? (string.IsNullOrWhiteSpace(envBaseUrl) ? "http://localhost:5000" : envBaseUrl.Trim())
            : configuredBaseUrl.Trim();

        builder.Services.Configure<BookshelfApiOptions>(options =>
        {
            options.BaseUrl = apiBaseUrl;
        });

        builder.Services.AddSingleton(serviceProvider =>
        {
            return new HttpClient
            {
                BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute),
            };
        });

        var offlineDbPath = Path.Combine(FileSystem.Current.AppDataDirectory, "bookshelf-offline.db");
        builder.Services.AddSingleton(new OfflineStore(offlineDbPath));
        builder.Services.AddSingleton<IConnectivityState, MauiConnectivityState>();
        builder.Services.AddSingleton<BookshelfApiClient>();
        builder.Services.AddSingleton<IBookshelfApiClient, OfflineBookshelfApiClient>();
        builder.Services.AddSingleton<ILocalMediaIndexService, LocalMediaIndexService>();
        builder.Services.AddSingleton<IOfflineSyncService, MauiOfflineSyncService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

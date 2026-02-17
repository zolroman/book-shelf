using Bookshelf.App.Services;

namespace Bookshelf.App;

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
        builder.Services.AddSingleton<IOfflineCacheService, OfflineCacheService>();
        builder.Services.AddSingleton<ISessionCheckpointStore, SqliteSessionCheckpointStore>();
        builder.Services.AddSingleton<IReadingSessionService, ReadingSessionService>();
        builder.Services.AddSingleton(_ => new HttpClient
        {
            BaseAddress = ResolveApiBaseAddress(),
            Timeout = TimeSpan.FromSeconds(10)
        });
        builder.Services.AddSingleton<IBookshelfApiClient, BookshelfApiClient>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        return builder.Build();
    }

    private static Uri ResolveApiBaseAddress()
    {
#if ANDROID
        return new Uri("http://10.0.2.2:5281/");
#else
        return new Uri("http://localhost:5281/");
#endif
    }
}

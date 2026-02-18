using Bookshelf.Shared.Client;
using Microsoft.Extensions.Logging;

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
        builder.Services.AddScoped<UserSessionState>();

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
        builder.Services.AddScoped<IBookshelfApiClient, BookshelfApiClient>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

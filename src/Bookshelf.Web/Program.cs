using Bookshelf.Web.Components;
using Bookshelf.Shared.Client;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<BookshelfApiOptions>(builder.Configuration.GetSection("BookshelfApi"));
builder.Services.AddScoped<UserSessionState>();
builder.Services.AddHttpClient<IBookshelfApiClient, BookshelfApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<BookshelfApiOptions>>().Value;
    var configuredBaseUrl = options.BaseUrl?.Trim();
    var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
        ? "http://localhost:5000"
        : configuredBaseUrl;
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Bookshelf.Shared._Imports).Assembly);

app.Run();

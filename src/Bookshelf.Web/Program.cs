using Bookshelf.Web.Components;
using Bookshelf.Shared.Contracts.Sync;
using Bookshelf.Web.Services;
using Bookshelf.Shared.UI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add HttpClient for API calls
builder.Services.AddHttpClient("BookshelfApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5281/");
    client.Timeout = TimeSpan.FromSeconds(1000);
});

builder.Services.AddScoped<IOfflineSyncService, WebOfflineSyncService>();
builder.Services.AddScoped<IBookshelfApiClient, WebBookshelfApiClient>();
builder.Services.AddScoped<IReadingSessionService, WebReadingSessionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        typeof(Bookshelf.Shared.UI._Imports).Assembly);

app.Run();

using Bookshelf.Application;
using Bookshelf.Api.Api;
using Bookshelf.Api.Api.Auth;
using Bookshelf.Api.Api.Middleware;
using Bookshelf.Infrastructure;
using Bookshelf.Shared.Contracts.System;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddBookshelfApplication();
builder.Services.AddBookshelfInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DownloadJobSyncWorker>();
builder.Services
    .AddAuthentication("Bearer")
    .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>("Bearer", _ => { });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapV1Endpoints();

app.MapGet("/api/v1/system/ping", () =>
{
    return Results.Ok(new PingResponse("bookshelf-api", DateTimeOffset.UtcNow));
});

app.Run();

public partial class Program;

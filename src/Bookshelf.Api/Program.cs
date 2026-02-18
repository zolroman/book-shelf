using Bookshelf.Application;
using Bookshelf.Api.Api;
using Bookshelf.Api.Api.Middleware;
using Bookshelf.Infrastructure;
using Bookshelf.Shared.Contracts.System;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddBookshelfApplication();
builder.Services.AddBookshelfInfrastructure(builder.Configuration);
builder.Services.AddSingleton<InMemoryApiStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ErrorHandlingMiddleware>();

app.MapHealthChecks("/health");
app.MapV1Endpoints();

app.MapGet("/api/v1/system/ping", () =>
{
    return Results.Ok(new PingResponse("bookshelf-api", DateTimeOffset.UtcNow));
});

app.Run();

public partial class Program;

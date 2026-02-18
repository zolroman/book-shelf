using Bookshelf.Application;
using Bookshelf.Infrastructure;
using Bookshelf.Shared.Contracts.System;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddBookshelfApplication();
builder.Services.AddBookshelfInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");

app.MapGet("/api/v1/system/ping", () =>
{
    return Results.Ok(new PingResponse("bookshelf-api", DateTimeOffset.UtcNow));
});

app.Run();

public partial class Program;

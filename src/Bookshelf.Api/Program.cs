using Bookshelf.Api.Middleware;
using Bookshelf.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5281");
builder.Services.AddControllers();
builder.Services.AddBookshelfInfrastructure();

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

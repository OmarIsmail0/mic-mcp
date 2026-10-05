using Company.McpServer.Authentication;
using Company.McpServer.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpApplication(builder.Configuration);

var app = builder.Build();

// The local HTTP URL is used directly during development; elsewhere redirect to HTTPS so the
// API key never travels in clear text.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Only paths under /mcp require the API key; /health stays open.
app.UseMiddleware<ApiKeyMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapMcp(ApiKeyConstants.ProtectedPath);

app.Run();

// Exposes the entry point to WebApplicationFactory in the test project.
public partial class Program;

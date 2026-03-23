using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ScreenShotNet;

DpiAwarenessService.EnableBestAvailableDpiAwareness();

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "screenshot-net",
            Version = "1.0.1",
            Title = "ScreenShotNet MCP",
            Description = "Captures Windows screen regions and returns screenshot image content directly."
        };
    })
    .WithStdioServerTransport()
    .WithListResourcesHandler((_, _) => ValueTask.FromResult(new ListResourcesResult
    {
        Resources = []
    }))
    .WithListResourceTemplatesHandler((_, _) => ValueTask.FromResult(new ListResourceTemplatesResult
    {
        ResourceTemplates = []
    }))
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

using Microsoft.MSBuild.ReleaseSnappingMCP;
using Microsoft.MSBuild.ReleaseSnappingMCP.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

builder.Services.AddSingleton<GitHubService>();
builder.Services.AddSingleton<ReleaseChecklistGenerator>();

var app = builder.Build();

await app.RunAsync();

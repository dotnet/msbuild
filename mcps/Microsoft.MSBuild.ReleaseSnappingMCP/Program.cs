using Microsoft.MSBuild.ReleaseSnappingMCP;
using Microsoft.MSBuild.ReleaseSnappingMCP.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

// Check for CLI mode (direct command execution)
if (args.Length > 0)
{
    await RunCliMode(args);
    return;
}

// Default: Run as MCP server
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

builder.Services.AddSingleton<GitHubService>();
builder.Services.AddSingleton<ReleaseChecklistGenerator>();

var app = builder.Build();

await app.RunAsync();

/// <summary>
/// CLI mode for direct command execution without MCP protocol.
/// </summary>
static async Task RunCliMode(string[] args)
{
    var command = args[0].ToLowerInvariant();
    
    switch (command)
    {
        case "create-issue":
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: create-issue <version> [labels]");
                Console.WriteLine("Example: create-issue 18.4");
                Console.WriteLine("\nRequires GITHUB_TOKEN environment variable to be set.");
                return;
            }
            await CreateReleaseIssue(args[1], args.Length > 2 ? args[2] : null);
            break;
            
        case "preview":
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: preview <version>");
                Console.WriteLine("Example: preview 18.4");
                return;
            }
            PreviewChecklist(args[1]);
            break;
            
        case "generate":
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: generate <version>");
                Console.WriteLine("Example: generate 18.4");
                return;
            }
            GenerateChecklist(args[1]);
            break;
            
        case "help":
        case "--help":
        case "-h":
            PrintHelp();
            break;
            
        default:
            Console.WriteLine($"Unknown command: {command}");
            PrintHelp();
            break;
    }
}

static void PrintHelp()
{
    Console.WriteLine("""
        MSBuild Release Snapping MCP
        
        Usage: dotnet run -- <command> [options]
        
        Commands:
          create-issue <version> [labels]  Create a GitHub issue with the release checklist
          preview <version>                Preview the checklist without creating an issue
          generate <version>               Generate and output the raw checklist markdown
          help                             Show this help message
        
        Examples:
          dotnet run -- create-issue 18.4
          dotnet run -- preview 18.4
          dotnet run -- generate 18.4
        
        Environment Variables:
          GITHUB_TOKEN    Required for create-issue command. Must have 'repo' scope.
        
        Running without arguments starts the MCP server for VS Code integration.
        """);
}

static async Task CreateReleaseIssue(string version, string? labels)
{
    var github = new GitHubService();
    var generator = new ReleaseChecklistGenerator();
    
    if (!github.IsAuthenticated)
    {
        Console.WriteLine("Error: GITHUB_TOKEN environment variable not set or invalid.");
        Console.WriteLine("Please set it with: $env:GITHUB_TOKEN = 'ghp_your_token'");
        return;
    }
    
    try
    {
        Console.WriteLine($"Creating release checklist issue for MSBuild {version}...");
        
        var releaseVersion = new ReleaseVersion(version);
        var checklist = generator.GenerateChecklist(releaseVersion);
        var title = ReleaseChecklistGenerator.GetIssueTitle(releaseVersion);
        
        var labelArray = string.IsNullOrWhiteSpace(labels) 
            ? null 
            : labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        var issue = await github.CreateIssueAsync(title, checklist, labelArray);
        
        Console.WriteLine();
        Console.WriteLine("✅ Successfully created release checklist issue!");
        Console.WriteLine();
        Console.WriteLine($"  Issue:  #{issue.Number}");
        Console.WriteLine($"  Title:  {issue.Title}");
        Console.WriteLine($"  URL:    {issue.HtmlUrl}");
        Console.WriteLine();
        Console.WriteLine("Version details:");
        Console.WriteLine($"  Current:  {releaseVersion.Current}");
        Console.WriteLine($"  Previous: {releaseVersion.Previous}");
        Console.WriteLine($"  Next:     {releaseVersion.Next}");
        Console.WriteLine($"  Branch:   {releaseVersion.BranchName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error creating issue: {ex.Message}");
    }
}

static void PreviewChecklist(string version)
{
    var generator = new ReleaseChecklistGenerator();
    var releaseVersion = new ReleaseVersion(version);
    
    Console.WriteLine($"📋 Release Checklist Preview for MSBuild {version}");
    Console.WriteLine();
    Console.WriteLine("Version Details:");
    Console.WriteLine($"  Current Version:    {releaseVersion.Current}");
    Console.WriteLine($"  Previous Version:   {releaseVersion.Previous}");
    Console.WriteLine($"  Next Version:       {releaseVersion.Next}");
    Console.WriteLine($"  Release Branch:     {releaseVersion.BranchName}");
    Console.WriteLine($"  DARC Channel:       {releaseVersion.DarcChannel}");
    Console.WriteLine($"  VS Rel Branch:      {releaseVersion.VsRelBranch}");
    Console.WriteLine();
    Console.WriteLine("Issue Title: " + ReleaseChecklistGenerator.GetIssueTitle(releaseVersion));
    Console.WriteLine();
    Console.WriteLine("To create this issue, run:");
    Console.WriteLine($"  dotnet run -- create-issue {version}");
}

static void GenerateChecklist(string version)
{
    var generator = new ReleaseChecklistGenerator();
    var releaseVersion = new ReleaseVersion(version);
    var checklist = generator.GenerateChecklist(releaseVersion);
    Console.WriteLine(checklist);
}

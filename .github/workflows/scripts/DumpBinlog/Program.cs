// Dumps overview / errors / warnings from a *.binlog file as JSON,
// by speaking the MCP stdio protocol to the locally-installed binlog-mcp
// dotnet global tool. Uses the official ModelContextProtocol C# SDK.
//
// Usage:
//   dotnet run --project .github/workflows/scripts/DumpBinlog -- <binlog-path> [<output-dir>]
//
// Writes (best-effort — missing tools or parse failures are tolerated):
//   <output-dir>/binlog-overview.json
//   <output-dir>/binlog-errors.json
//   <output-dir>/binlog-warnings.json

using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: DumpBinlog <binlog-path> [<output-dir>]");
    return 1;
}

var binlogPath = Path.GetFullPath(args[0]);
var outputDir = args.Length > 1 ? args[1] : "/tmp";

if (!File.Exists(binlogPath))
{
    Console.Error.WriteLine($"Binlog not found: {binlogPath}");
    return 1;
}

Directory.CreateDirectory(outputDir);

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

try
{
    var clientTransport = new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "DumpBinlog",
        Command = "binlog-mcp",
        Arguments = [],
    });

    await using var client = await McpClient.CreateAsync(clientTransport);

    await DumpTool(client, "binlog_overview",
        new Dictionary<string, object?> { ["binlog_file"] = binlogPath },
        Path.Combine(outputDir, "binlog-overview.json"));

    await DumpTool(client, "binlog_errors",
        new Dictionary<string, object?> { ["binlog_file"] = binlogPath },
        Path.Combine(outputDir, "binlog-errors.json"));

    await DumpTool(client, "binlog_warnings",
        new Dictionary<string, object?> { ["binlog_file"] = binlogPath, ["top"] = 10 },
        Path.Combine(outputDir, "binlog-warnings.json"));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"fatal: {ex}");

    // Guarantee the three output files exist so downstream `cat` steps and the
    // agent always have something structured to read. The agent treats an
    // `{ "error": ... }` payload as "tool failed; fall back to /tmp/build-output.log".
    var fatal = new { error = $"DumpBinlog fatal: {ex.Message}" };
    var fatalJson = JsonSerializer.Serialize(fatal, jsonOptions);
    foreach (var name in new[] { "binlog-overview.json", "binlog-errors.json", "binlog-warnings.json" })
    {
        var path = Path.Combine(outputDir, name);
        if (!File.Exists(path))
        {
            try { File.WriteAllText(path, fatalJson); } catch { }
        }
    }
    return 1;
}

return 0;

async Task DumpTool(McpClient client, string toolName, Dictionary<string, object?> args, string outputPath)
{
    try
    {
        var result = await client.CallToolAsync(toolName, args, cancellationToken: CancellationToken.None);
        var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

        // Try to parse as JSON for pretty output; fall back to raw text
        object? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<JsonElement>(text);
        }
        catch
        {
            parsed = text;
        }

        File.WriteAllText(outputPath, JsonSerializer.Serialize(parsed, jsonOptions));
        Console.Error.WriteLine($"wrote {outputPath}");
    }
    catch (Exception ex)
    {
        var error = new { error = $"{toolName} failed: {ex.Message}" };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(error, jsonOptions));
        Console.Error.WriteLine($"{toolName} failed: {ex.Message}");
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// The class allows to modify base image configuration.
/// </summary>
internal sealed class ImageConfig
{
    private readonly JsonObject _config;
    private readonly Dictionary<string, string> _labels;
    private readonly HashSet<Port> _exposedPorts;
    private readonly Dictionary<string, string> _environmentVariables;
    private string? _newWorkingDirectory;
    private string[]? _newEntrypoint;
    private string[]? _newCmd;
    private string? _user;

    /// <summary>
    /// Models the file system of the image. Typically has a key 'type' with value 'layers' and a key 'diff_ids' with a list of layer digests.
    /// </summary>
    private readonly List<string> _rootFsLayers;
    private readonly string _architecture;
    private readonly string _os;
    private readonly List<HistoryEntry> _history;

    /// <summary>
    /// Gets a value indicating whether the base image is has a Windows operating system.
    /// </summary>
    public bool IsWindows => "windows".Equals(_os, StringComparison.OrdinalIgnoreCase);

    public ReadOnlyDictionary<string, string> EnvironmentVariables => _environmentVariables.AsReadOnly();
    public HashSet<Port> Ports => _exposedPorts;

    internal ImageConfig(string imageConfigJson) : this(JsonNode.Parse(imageConfigJson)!)
    {
    }

    internal ImageConfig(JsonNode config)
    {
        _config = config as JsonObject ?? throw new ArgumentException($"{nameof(config)} should be a JSON object.", nameof(config));
        if (_config["config"] is not JsonObject)
        {
            throw new ArgumentException("Base image configuration should contain a 'config' node.");
        }

        _labels = GetLabels();
        _exposedPorts = GetExposedPorts();
        _environmentVariables = GetEnvironmentVariables();
        _rootFsLayers = GetRootFileSystemLayers();
        _architecture = GetArchitecture();
        _os = GetOs();
        _history = GetHistory();
        _user = GetUser();
        _newEntrypoint = GetEntrypoint();
        _newCmd = GetCmd();
    }

    // Return values from the base image config.
    internal string? GetUser() => _config["config"]?["User"]?.ToString();
    internal string[]? GetEntrypoint() => _config["config"]?["Entrypoint"]?.AsArray()?.Select(node => node!.GetValue<string>())?.ToArray();
    private string[]? GetCmd() => _config["config"]?["Entrypoint"]?.AsArray()?.Select(node => node!.GetValue<string>())?.ToArray();
    private List<HistoryEntry> GetHistory() => _config["history"]?.AsArray().Select(node => node.Deserialize<HistoryEntry>()!).ToList() ?? new List<HistoryEntry>();
    private string GetOs() => _config["os"]?.ToString() ?? throw new ArgumentException("Base image configuration should contain an 'os' property.");
    private string GetArchitecture() => _config["architecture"]?.ToString() ?? throw new ArgumentException("Base image configuration should contain an 'architecture' property.");

    /// <summary>
    /// Builds in additional configuration and returns updated image configuration in JSON format as string.
    /// </summary>
    internal string BuildConfig()
    {
        var newConfig = new JsonObject();

        if (_exposedPorts.Any())
        {
            newConfig["ExposedPorts"] = CreatePortMap();
        }
        if (_labels.Any())
        {
            newConfig["Labels"] = CreateLabelMap();
        }
        if (_environmentVariables.Count != 0)
        {
            newConfig["Env"] = CreateEnvironmentVariablesMapping();
        }

        if (_newWorkingDirectory is not null)
        {
            newConfig["WorkingDir"] = _newWorkingDirectory;
        }

        if (_newEntrypoint?.Length > 0)
        {
            newConfig["Entrypoint"] = ToJsonArray(_newEntrypoint);
        }

        if (_newCmd?.Length > 0)
        {
            newConfig["Cmd"] = ToJsonArray(_newCmd);
        }

        if (_user is not null)
        {
            newConfig["User"] = _user;
        }

        // These fields aren't (yet) supported by the task layer, but we should
        // preserve them if they're already set in the base image.
        foreach (string propertyName in new[] { "Volumes", "StopSignal" })
        {
            if (_config["config"]?[propertyName] is JsonNode propertyValue)
            {
                // we can't just copy the property value because JsonValues have Parents
                // and they cannot be re-parented. So we need to Clone them, but there's
                // not an API for cloning, so the recommendation is to stringify and parse.
                newConfig[propertyName] = JsonNode.Parse(propertyValue.ToJsonString());
            }
        }

        // Add history entries for ourselves so folks can map generated layers to the Dockerfile commands.
        // The number of (non empty) history items must match the number of layers in the image.
        // Some registries like JFrog Artifactory have there a strict validation rule (see sdk-container-builds#382).
        int numberOfLayers = _rootFsLayers.Count;
        int numberOfNonEmptyLayerHistoryEntries = _history.Count(h => h.empty_layer is null or false);
        int missingHistoryEntries = numberOfLayers - numberOfNonEmptyLayerHistoryEntries;
        HistoryEntry customHistoryEntry = new(created: DateTime.UtcNow, author: ".NET SDK",
            created_by: $".NET SDK Container Tooling, version {Constants.Version}");
        for (int i = 0; i < missingHistoryEntries; i++)
        {
            _history.Add(customHistoryEntry);
        }

        var configContainer = new JsonObject()
        {
            ["config"] = newConfig,
            //update creation date
            ["created"] = RFC3339Format(DateTime.UtcNow),
            ["rootfs"] = new JsonObject()
            {
                ["type"] = "layers",
                ["diff_ids"] = ToJsonArray(_rootFsLayers)
            },
            ["architecture"] = _architecture,
            ["os"] = _os,
            ["history"] = new JsonArray(_history.Select(CreateHistory).ToArray<JsonNode>())
        };

        return configContainer.ToJsonString();

        static JsonArray ToJsonArray(IEnumerable<string> items) => new(items.Where(s => !string.IsNullOrEmpty(s)).Select(s => JsonValue.Create(s)).ToArray<JsonNode?>());
    }

    private JsonObject CreateHistory(HistoryEntry h)
    {
        var history = new JsonObject();

        if (h.author is not null)
        {
            history["author"] = h.author;
        }
        if (h.comment is not null)
        {
            history["comment"] = h.comment;
        }
        if (h.created is { } date)
        {
            history["created"] = RFC3339Format(date);
        }
        if (h.created_by is not null)
        {
            history["created_by"] = h.created_by;
        }
        if (h.empty_layer is not null)
        {
            history["empty_layer"] = h.empty_layer;
        }

        return history;
    }

    static string RFC3339Format(DateTimeOffset dateTime) => dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", System.Globalization.CultureInfo.InvariantCulture);

    internal void ExposePort(int number, PortType type)
    {
        _exposedPorts.Add(new(number, type));
    }

    internal void AddEnvironmentVariable(string envVarName, string value)
    {
        _environmentVariables[envVarName] = value;
    }

    internal void AddLabel(string name, string value)
    {
        _labels[name] = value;
    }

    internal void SetWorkingDirectory(string workingDirectory)
    {
        _newWorkingDirectory = workingDirectory;
    }

    internal void SetEntrypointAndCmd(string[] entrypoint, string[] cmd)
    {
        _newEntrypoint = entrypoint;
        _newCmd = cmd;
    }

    internal void AddLayer(Layer l)
    {
        _rootFsLayers.Add(l.Descriptor.UncompressedDigest!);
    }

    internal void SetUser(string user) => _user = user;

    private HashSet<Port> GetExposedPorts()
    {
        HashSet<Port> ports = new();
        if (_config["config"]?["ExposedPorts"] is JsonObject portsJson)
        {
            // read label mappings from object
            foreach (KeyValuePair<string, JsonNode?> property in portsJson)
            {
                if (property.Key is { } propertyName
                    && property.Value is JsonObject propertyValue
                    && ContainerHelpers.TryParsePort(propertyName, out Port? parsedPort, out ContainerHelpers.ParsePortError? _))
                {
                    ports.Add(parsedPort.Value);
                }
            }
        }
        return ports;
    }

    private Dictionary<string, string> GetLabels()
    {
        Dictionary<string, string> labels = new();
        if (_config["config"]?["Labels"] is JsonObject labelsJson)
        {
            // read label mappings from object
            foreach (KeyValuePair<string, JsonNode?> property in labelsJson)
            {
                if (property.Key is { } propertyName && property.Value is JsonValue propertyValue)
                {
                    labels[propertyName] = propertyValue.ToString();
                }
            }
        }
        return labels;
    }

    private Dictionary<string, string> GetEnvironmentVariables()
    {
        Dictionary<string, string> envVars = new();
        if (_config["config"]?["Env"] is JsonArray envVarJson)
        {
            foreach (JsonNode? entry in envVarJson)
            {
                if (entry is null)
                    continue;

                string[] val = entry.GetValue<string>().Split('=', 2);

                if (val.Length != 2)
                    continue;

                envVars.Add(val[0], val[1]);
            }
        }
        return envVars;
    }

    private JsonObject CreatePortMap()
    {
        // ports are entries in a key/value map whose keys are "<number>/<type>" and whose values are an empty object.
        // yes, this is odd.
        JsonObject container = new();
        foreach (Port port in _exposedPorts)
        {
            container.Add($"{port.Number}/{port.Type}", new JsonObject());
        }
        return container;
    }

    private JsonArray CreateEnvironmentVariablesMapping()
    {
        // Env is a JSON array where each value is of the format: "VAR=value"
        JsonArray envVarJson = new();
        foreach (KeyValuePair<string, string> envVar in _environmentVariables)
        {
            envVarJson.Add($"{envVar.Key}={envVar.Value}");
        }
        return envVarJson;
    }

    private JsonObject CreateLabelMap()
    {
        JsonObject container = new();
        foreach (KeyValuePair<string, string> label in _labels)
        {
            container.Add(label.Key, label.Value);
        }
        return container;
    }

    private List<string> GetRootFileSystemLayers()
    {
        if (_config["rootfs"] is { } rootfs)
        {
            if (rootfs["type"]?.GetValue<string>() == "layers" && rootfs["diff_ids"] is JsonArray layers)
            {
                return layers.Select(l => l!.GetValue<string>()).ToList();
            }
            else
            {
                return new();
            }
        }
        else
        {
            throw new InvalidOperationException("Base image configuration should contain a 'rootfs' node.");
        }
    }

    private record HistoryEntry(DateTimeOffset? created = null, string? created_by = null, bool? empty_layer = null, string? comment = null, string? author = null);
}

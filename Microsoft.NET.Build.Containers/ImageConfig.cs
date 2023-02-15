// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// The class allows to modify base image configuration.
/// </summary>
internal sealed class ImageConfig
{
    private readonly JsonObject _config;
    private readonly Dictionary<string, string> _labels;
    private readonly List<Layer> _newLayers = new();
    private readonly HashSet<Port> _exposedPorts;
    private readonly Dictionary<string, string> _environmentVariables;
    private string? _newWorkingDirectory;
    private (string[] ExecutableArgs, string[]? Args)? _newEntryPoint;

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
    }

    /// <summary>
    /// Builds in additional configuration and returns updated image configuration in JSON format as string.
    /// </summary>
    internal string BuildConfig()
    {
        if (_config["config"] is not JsonObject config)
        {
            throw new InvalidOperationException("Base image configuration should contain a 'config' node.");
        }

        //update creation date
        _config["created"] = DateTime.UtcNow;
  
        config["ExposedPorts"] = CreatePortMap();
        config["Env"] = CreateEnvironmentVariablesMapping();
        config["Labels"] = CreateLabelMap();

        if (_newWorkingDirectory is not null)
        {
            config["WorkingDir"] = _newWorkingDirectory;
        }

        if (_newEntryPoint.HasValue)
        {
            config["Entrypoint"] = ToJsonArray(_newEntryPoint.Value.ExecutableArgs);

            if (_newEntryPoint.Value.Args is null)
            {
                config.Remove("Cmd");
            }
            else
            {
                config["Cmd"] = ToJsonArray(_newEntryPoint.Value.Args);
            }
        }

        foreach (Layer l in _newLayers)
        {
            _config["rootfs"]!["diff_ids"]!.AsArray().Add(l.Descriptor.UncompressedDigest);
        }

        return _config.ToJsonString();
        static JsonArray ToJsonArray(string[] items) => new(items.Where(s => !string.IsNullOrEmpty(s)).Select(s => JsonValue.Create(s)).ToArray());
    }

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

    internal void SetEntryPoint(string[] executableArgs, string[]? args = null)
    {
        _newEntryPoint = (executableArgs, args);
    }

    internal void AddLayer(Layer l)
    {
        _newLayers.Add(l);
    }

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
}

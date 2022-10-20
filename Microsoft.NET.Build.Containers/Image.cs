using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

public class Image
{
    public JsonNode manifest;
    public JsonNode config;

    public readonly string OriginatingName;
    internal readonly Registry? originatingRegistry;

    internal List<Layer> newLayers = new();

    private HashSet<Label> labels;

    internal HashSet<Port> exposedPorts;

    internal Dictionary<string, string> environmentVariables;

    public Image(JsonNode manifest, JsonNode config, string name, Registry? registry)
    {
        this.manifest = manifest;
        this.config = config;
        this.OriginatingName = name;
        this.originatingRegistry = registry;
        // these next values are inherited from the parent image, so we need to seed our new image with them.
        this.labels = ReadLabelsFromConfig(config);
        this.exposedPorts = ReadPortsFromConfig(config);
        this.environmentVariables = ReadEnvVarsFromConfig(config);
    }

    public IEnumerable<Descriptor> LayerDescriptors
    {
        get
        {
            JsonNode? layersNode = manifest["layers"];

            if (layersNode is null)
            {
                throw new NotImplementedException("Tried to get layer information but there is no layer node?");
            }

            foreach (JsonNode? descriptorJson in layersNode.AsArray())
            {
                if (descriptorJson is null)
                {
                    throw new NotImplementedException("Null layer descriptor in the list?");
                }

                yield return descriptorJson.Deserialize<Descriptor>();
            }
        }
    }

    public void AddLayer(Layer l)
    {
        newLayers.Add(l);
        manifest["layers"]!.AsArray().Add(l.Descriptor);
        config["rootfs"]!["diff_ids"]!.AsArray().Add(l.Descriptor.UncompressedDigest);
        RecalculateDigest();
    }

    private void RecalculateDigest()
    {
        config["created"] = DateTime.UtcNow;

        manifest["config"]!["digest"] = GetDigest(config);
        manifest["config"]!["size"] = Encoding.UTF8.GetBytes(config.ToJsonString()).Length;
    }

    private JsonObject CreatePortMap()
    {
        // ports are entries in a key/value map whose keys are "<number>/<type>" and whose values are an empty object.
        // yes, this is odd.
        var container = new JsonObject();
        foreach (var port in exposedPorts)
        {
            container.Add($"{port.number}/{port.type}", new JsonObject());
        }
        return container;
    }

    private JsonArray CreateEnvironmentVarMapping()
    {
        // Env is a JSON array where each value is of the format: "VAR=value"
        var envVarJson = new JsonArray();
        foreach (var envVar in environmentVariables)
        {
            envVarJson.Add<string>($"{envVar.Key}={envVar.Value}");
        }
        return envVarJson;
    }

    private static HashSet<Label> ReadLabelsFromConfig(JsonNode inputConfig)
    {
        if (inputConfig is JsonObject config && config["Labels"] is JsonObject labelsJson)
        {
            // read label mappings from object
            var labels = new HashSet<Label>();
            foreach (var property in labelsJson)
            {
                if (property.Key is { } propertyName && property.Value is JsonValue propertyValue)
                {
                    labels.Add(new(propertyName, propertyValue.ToString()));
                }
            }
            return labels;
        }
        else
        {
            // initialize an empty labels map
            return new HashSet<Label>();
        }
    }

    private static Dictionary<string, string> ReadEnvVarsFromConfig(JsonNode inputConfig)
    {
        if (inputConfig is JsonObject config && config["config"]!["Env"] is JsonArray envVarJson)
        {
            var envVars = new Dictionary<string, string>();
            foreach (var entry in envVarJson)
            {
                if (entry is null)
                    continue;

                var val = entry.GetValue<string>().Split('=', 2);

                if (val.Length != 2)
                    continue;

                envVars.Add(val[0], val[1]);
            }
            return envVars;
        }
        else
        {
            return new Dictionary<string, string>();
        }
    }

    private static HashSet<Port> ReadPortsFromConfig(JsonNode inputConfig)
    {
        if (inputConfig is JsonObject config && config["ExposedPorts"] is JsonObject portsJson)
        {
            // read label mappings from object
            var ports = new HashSet<Port>();
            foreach (var property in portsJson)
            {
                if (property.Key is { } propertyName
                    && property.Value is JsonObject propertyValue
                    && ContainerHelpers.TryParsePort(propertyName, out var parsedPort, out var _))
                {
                    ports.Add(parsedPort);
                }
            }
            return ports;
        }
        else
        {
            // initialize an empty ports map
            return new HashSet<Port>();
        }
    }

    private JsonObject CreateLabelMap()
    {
        var container = new JsonObject();
        foreach (var label in labels)
        {
            container.Add(label.name, label.value);
        }
        return container;
    }

    static JsonArray ToJsonArray(string[] items) => new JsonArray(items.Where(s => !string.IsNullOrEmpty(s)).Select(s => JsonValue.Create(s)).ToArray());

    public void SetEntrypoint(string[] executableArgs, string[]? args = null)
    {
        JsonObject? configObject = config["config"]!.AsObject();

        if (configObject is null)
        {
            throw new NotImplementedException("Expected base image to have a config node");
        }

        configObject["Entrypoint"] = ToJsonArray(executableArgs);

        if (args is null)
        {
            configObject.Remove("Cmd");
        }
        else
        {
            configObject["Cmd"] = ToJsonArray(args);
        }

        RecalculateDigest();
    }

    public string WorkingDirectory
    {
        get => (string?)config["config"]!["WorkingDir"] ?? "";
        set
        {
            config["config"]!["WorkingDir"] = value;
            RecalculateDigest();
        }
    }

    public void Label(string name, string value)
    {
        labels.Add(new(name, value));
        config["config"]!["Labels"] = CreateLabelMap();
        RecalculateDigest();
    }

    public void AddEnvironmentVariable(string envVarName, string value)
    {
        if (!environmentVariables.ContainsKey(envVarName))
        {
            environmentVariables.Add(envVarName, value);
        }
        else
        {
            environmentVariables[envVarName] = value;
        }
        config["config"]!["Env"] = CreateEnvironmentVarMapping();
        RecalculateDigest();
    }

    public void ExposePort(int number, PortType type)
    {
        exposedPorts.Add(new(number, type));
        config["config"]!["ExposedPorts"] = CreatePortMap();
        RecalculateDigest();
    }

    public string GetDigest(JsonNode json)
    {
        string hashString;

        hashString = GetSha(json);

        return $"sha256:{hashString}";
    }

    public static string GetSha(JsonNode json)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(json.ToJsonString()), hash);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

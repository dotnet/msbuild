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

    public Image(JsonNode manifest, JsonNode config, string name, Registry? registry)
    {
        this.manifest = manifest;
        this.config = config;
        this.OriginatingName = name;
        this.originatingRegistry = registry;
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
        config["rootfs"]!["diff_ids"]!.AsArray().Add(l.Descriptor.Digest); // TODO: this should be the descriptor of the UNCOMPRESSED tarball (once we turn on compression)
        RecalculateDigest();
    }

    private void RecalculateDigest() {
        manifest["config"]!["digest"] = GetDigest(config);
    }

    static JsonArray ToJsonArray(string[] items) => new JsonArray(items.Where(s => !string.IsNullOrEmpty(s)).Select(s =>(JsonValue) s).ToArray());

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

    public string WorkingDirectory {
        get => (string?)manifest["config"]!["WorkingDir"] ?? "";
        set {
            config["config"]!["WorkingDir"] = value;
            RecalculateDigest();
        }
    }

    public string GetDigest(JsonNode json)
    {
        string hashString;

        hashString = GetSha(json);

        return $"sha256:{hashString}";
    }

    public static string GetSha(JsonNode json)
    {
        using SHA256 mySHA256 = SHA256.Create();
        byte[] hash = mySHA256.ComputeHash(Encoding.UTF8.GetBytes(json.ToJsonString()));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

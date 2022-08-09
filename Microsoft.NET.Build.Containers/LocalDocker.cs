using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

public class LocalDocker
{
    public static async Task Load(Image x, string name, string tag, string baseName)
    {
        // call `docker load` and get it ready to recieve input
        ProcessStartInfo loadInfo = new("docker", $"load");
        loadInfo.RedirectStandardInput = true;
        loadInfo.RedirectStandardOutput = true;

        using Process? loadProcess = Process.Start(loadInfo);

        if (loadProcess is null)
        {
            throw new NotImplementedException("Failed creating docker process");
        }

        // Create new stream tarball

        await WriteImageToStream(x, name, tag, loadProcess.StandardInput.BaseStream);

        loadProcess.StandardInput.Close();

        await loadProcess.WaitForExitAsync();
    }

    public static async Task WriteImageToStream(Image x, string name, string tag, Stream imageStream)
    {
        TarWriter writer = new(imageStream, TarEntryFormat.Gnu, leaveOpen: true);


        // Feed each layer tarball into the stream
        JsonArray layerTarballPaths = new JsonArray();

        foreach (var d in x.LayerDescriptors)
        {
            if (!x.originatingRegistry.HasValue)
            {
                throw new NotImplementedException("Need a good error for 'couldn't download a thing because no link to registry'");
            }

            string localPath = await x.originatingRegistry.Value.DownloadBlob(x.OriginatingName, d);

            // Stuff that (uncompressed) tarball into the image tar stream
            string layerTarballPath = $"{d.Digest.Substring("sha256:".Length)}/layer.tar";
            await writer.WriteEntryAsync(localPath, layerTarballPath);
            layerTarballPaths.Add(layerTarballPath);
        }

        // add config
        string configTarballPath = $"{Image.GetSha(x.config)}.json";

        using (MemoryStream configStream = new MemoryStream(Encoding.UTF8.GetBytes(x.config.ToJsonString())))
        {
            GnuTarEntry configEntry = new(TarEntryType.RegularFile, configTarballPath)
            {
                DataStream = configStream
            };

            await writer.WriteEntryAsync(configEntry);
        }

        // Add manifest
        JsonArray tagsNode = new()
        {
            name + ":" + tag
        };

        JsonNode manifestNode = new JsonArray(new JsonObject
        {
            { "Config", configTarballPath },
            { "RepoTags", tagsNode },
            { "Layers", layerTarballPaths }
        });

        using (MemoryStream manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(manifestNode.ToJsonString())))
        {
            GnuTarEntry manifestEntry = new(TarEntryType.RegularFile, "manifest.json")
            {
                DataStream = manifestStream
            };

            await writer.WriteEntryAsync(manifestEntry);
        }
    }
}

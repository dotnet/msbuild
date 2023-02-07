// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
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
        loadInfo.RedirectStandardError = true;

        using Process? loadProcess = Process.Start(loadInfo);

        if (loadProcess is null)
        {
            throw new NotImplementedException("Failed creating docker process");
        }

        // Create new stream tarball

        await WriteImageToStream(x, name, tag, loadProcess.StandardInput.BaseStream).ConfigureAwait(false);

        loadProcess.StandardInput.Close();

        await loadProcess.WaitForExitAsync().ConfigureAwait(false);

        if (loadProcess.ExitCode != 0)
        {
            throw new DockerLoadException($"Failed to load image to local Docker daemon. stdout: {await loadProcess.StandardError.ReadToEndAsync().ConfigureAwait(false)}");
        }
    }

    public static async Task WriteImageToStream(Image x, string name, string tag, Stream imageStream)
    {
        using TarWriter writer = new(imageStream, TarEntryFormat.Pax, leaveOpen: true);


        // Feed each layer tarball into the stream
        JsonArray layerTarballPaths = new JsonArray();

        foreach (var d in x.LayerDescriptors)
        {
            if (x.originatingRegistry is {} registry)
            {
                string localPath = await registry.DownloadBlob(x.OriginatingName, d).ConfigureAwait(false);

                // Stuff that (uncompressed) tarball into the image tar stream
                // TODO uncompress!!
                string layerTarballPath = $"{d.Digest.Substring("sha256:".Length)}/layer.tar";
                await writer.WriteEntryAsync(localPath, layerTarballPath).ConfigureAwait(false);
                layerTarballPaths.Add(layerTarballPath);
            }
            else
            {
                throw new NotImplementedException("Need a good error for 'couldn't download a thing because no link to registry'");
            }        }

        // add config
        string configTarballPath = $"{Image.GetSha(x.config)}.json";

        using (MemoryStream configStream = new MemoryStream(Encoding.UTF8.GetBytes(x.config.ToJsonString())))
        {
            PaxTarEntry configEntry = new(TarEntryType.RegularFile, configTarballPath)
            {
                DataStream = configStream
            };

            await writer.WriteEntryAsync(configEntry).ConfigureAwait(false);
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
            PaxTarEntry manifestEntry = new(TarEntryType.RegularFile, "manifest.json")
            {
                DataStream = manifestStream
            };

            await writer.WriteEntryAsync(manifestEntry).ConfigureAwait(false);
        }
    }
}

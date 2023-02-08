// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

public class LocalDocker : ILocalDaemon
{
    private readonly Action<string> logger;

    public LocalDocker(Action<string> logger)
    {
        this.logger = logger;
    }

    public async Task Load(Image image, ImageReference sourceReference, ImageReference destinationReference)
    {
        // call `docker load` and get it ready to receive input
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

        await WriteImageToStream(image, sourceReference, destinationReference, loadProcess.StandardInput.BaseStream).ConfigureAwait(false);

        loadProcess.StandardInput.Close();

        await loadProcess.WaitForExitAsync().ConfigureAwait(false);

        if (loadProcess.ExitCode != 0)
        {
            throw new DockerLoadException($"Failed to load image to local Docker daemon. stdout: {await loadProcess.StandardError.ReadToEndAsync().ConfigureAwait(false)}");
        }
    }

    public async Task<bool> IsAvailable()
    {
        try
        {
            var config = await GetConfig().ConfigureAwait(false);
            if (!config.RootElement.TryGetProperty("ServerErrors", out var errorProperty)) {
                return true;
            } else if (errorProperty.ValueKind == JsonValueKind.Array && errorProperty.GetArrayLength() == 0) {
                return true;
            } else {
                // we have errors, turn them into a string and log them
                var messages = String.Join(Environment.NewLine, errorProperty.EnumerateArray());
                logger($"The daemon server reported errors: {messages}");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger($"Error while reading daemon config: {ex}");
            return false;
        }
    }

    private static async Task<JsonDocument> GetConfig()
    {
        var psi = new ProcessStartInfo("docker", "info --format=\"{{json .}}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var proc = Process.Start(psi);
        if (proc is null) throw new Exception("Failed to start docker client process");
        await proc.WaitForExitAsync().ConfigureAwait(false);
        if (proc.ExitCode != 0) throw new Exception($"Failed to get docker info({proc.ExitCode})\n{await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false)}\n{await proc.StandardError.ReadToEndAsync().ConfigureAwait(false)}");
        return await JsonDocument.ParseAsync(proc.StandardOutput.BaseStream).ConfigureAwait(false);
    }

    private static async Task WriteImageToStream(Image image, ImageReference sourceReference, ImageReference destinationReference, Stream imageStream)
    {
        using TarWriter writer = new(imageStream, TarEntryFormat.Pax, leaveOpen: true);


        // Feed each layer tarball into the stream
        JsonArray layerTarballPaths = new JsonArray();

        foreach (var d in image.LayerDescriptors)
        {
            if (sourceReference.Registry is { } registry)
            {
                string localPath = await registry.DownloadBlob(sourceReference.Repository, d).ConfigureAwait(false);;

                // Stuff that (uncompressed) tarball into the image tar stream
                // TODO uncompress!!
                string layerTarballPath = $"{d.Digest.Substring("sha256:".Length)}/layer.tar";
                await writer.WriteEntryAsync(localPath, layerTarballPath).ConfigureAwait(false);
                layerTarballPaths.Add(layerTarballPath);
            }
            else
            {
                throw new NotImplementedException("Need a good error for 'couldn't download a thing because no link to registry'");
            }
        }

        // add config
        string configTarballPath = $"{Image.GetSha(image.config)}.json";

        using (MemoryStream configStream = new MemoryStream(Encoding.UTF8.GetBytes(image.config.ToJsonString())))
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
            destinationReference.RepositoryAndTag
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

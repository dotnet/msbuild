// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal sealed class LocalDocker : ILocalDaemon
{
    private readonly Action<string> logger;

    public LocalDocker(Action<string> logger)
    {
        this.logger = logger;
    }

    public async Task Load(BuiltImage image, ImageReference sourceReference, ImageReference destinationReference)
    {
        // call `docker load` and get it ready to receive input
        ProcessStartInfo loadInfo = new("docker", $"load");
        loadInfo.RedirectStandardInput = true;
        loadInfo.RedirectStandardOutput = true;
        loadInfo.RedirectStandardError = true;

        using Process? loadProcess = Process.Start(loadInfo);

        if (loadProcess is null)
        {
            throw new NotImplementedException(Resource.GetString(Strings.DockerProcessCreationFailed));
        }

        // Create new stream tarball

        await WriteImageToStream(image, sourceReference, destinationReference, loadProcess.StandardInput.BaseStream).ConfigureAwait(false);

        loadProcess.StandardInput.Close();

        await loadProcess.WaitForExitAsync().ConfigureAwait(false);

        if (loadProcess.ExitCode != 0)
        {
            throw new DockerLoadException(Resource.FormatString(nameof(Strings.ImageLoadFailed), await loadProcess.StandardError.ReadToEndAsync().ConfigureAwait(false)));
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
        if (proc is null) throw new Exception(Resource.GetString(nameof(Strings.DockerProcessCreationFailed)));
        await proc.WaitForExitAsync().ConfigureAwait(false);
        if (proc.ExitCode != 0) throw new Exception(Resource.FormatString(
            nameof(Strings.DockerInfoFailed),
            proc.ExitCode,
            await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false)));
        return await JsonDocument.ParseAsync(proc.StandardOutput.BaseStream).ConfigureAwait(false);
    }

    private static async Task WriteImageToStream(BuiltImage image, ImageReference sourceReference, ImageReference destinationReference, Stream imageStream)
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
                throw new NotImplementedException(Resource.GetString(nameof(Strings.MissingLinkToRegistry)));
            }
        }

        // add config
        string configTarballPath = $"{image.ImageSha}.json";

        using (MemoryStream configStream = new MemoryStream(Encoding.UTF8.GetBytes(image.Config)))
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

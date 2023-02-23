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

    public async Task LoadAsync(BuiltImage image, ImageReference sourceReference, ImageReference destinationReference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        await WriteImageToStreamAsync(image, sourceReference, destinationReference, loadProcess.StandardInput.BaseStream, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        loadProcess.StandardInput.Close();

        await loadProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        if (loadProcess.ExitCode != 0)
        {
            throw new DockerLoadException(Resource.FormatString(nameof(Strings.ImageLoadFailed), await loadProcess.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false)));
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        return IsAvailableIntAsync(sync: false, cancellationToken);
    }

    ///<inheritdoc/>
    public bool IsAvailable()
    {
        //it is safe to call Task.Result here, as when sync is true, the method is fully synchronous
        return IsAvailableIntAsync(sync: true, cancellationToken: default).Result;
    }

    private async Task<bool> IsAvailableIntAsync(bool sync, CancellationToken cancellationToken)
    {
        try
        {
            //it is safe to call Task.Result here, as when sync is true, the method is fully synchronous
            JsonDocument config = sync ? GetConfigAsync(sync: true, cancellationToken).Result : await GetConfigAsync(sync: false, cancellationToken).ConfigureAwait(false);

            if (!config.RootElement.TryGetProperty("ServerErrors", out JsonElement errorProperty))
            {
                return true;
            }
            else if (errorProperty.ValueKind == JsonValueKind.Array && errorProperty.GetArrayLength() == 0)
            {
                return true;
            }
            else
            {
                // we have errors, turn them into a string and log them
                string messages = string.Join(Environment.NewLine, errorProperty.EnumerateArray());
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

    /// <summary>
    /// Gets docker configuration.
    /// </summary>
    /// <param name="sync">when <see langword="true"/>, the method is executed synchronously.</param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="DockerLoadException">when failed to retrieve docker configuration.</exception>
    internal static async Task<JsonDocument> GetConfigAsync(bool sync, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var psi = new ProcessStartInfo("docker", "info --format=\"{{json .}}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        Process proc = Process.Start(psi) ?? throw new DockerLoadException(Resource.GetString(nameof(Strings.DockerProcessCreationFailed)));

        if (!sync)
        {
            await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            proc.WaitForExit();
        }

        cancellationToken.ThrowIfCancellationRequested();


        if (proc.ExitCode != 0)
        {
            if (!sync)
            {
                throw new DockerLoadException(Resource.FormatString(
                    nameof(Strings.DockerInfoFailed),
                    proc.ExitCode,
                    await proc.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false)));
            }
            else
            {
            	throw new DockerLoadException(Resource.FormatString(
                    nameof(Strings.DockerInfoFailed),
                    proc.ExitCode,
                    proc.StandardOutput.ReadToEnd()));
            }
        }

        if (!sync)
        {
            return await JsonDocument.ParseAsync(proc.StandardOutput.BaseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return JsonDocument.Parse(proc.StandardOutput.BaseStream);
        }
    }

    private static async Task WriteImageToStreamAsync(BuiltImage image, ImageReference sourceReference, ImageReference destinationReference, Stream imageStream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using TarWriter writer = new(imageStream, TarEntryFormat.Pax, leaveOpen: true);


        // Feed each layer tarball into the stream
        JsonArray layerTarballPaths = new JsonArray();

        foreach (var d in image.LayerDescriptors)
        {
            if (sourceReference.Registry is { } registry)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string localPath = await registry.DownloadBlobAsync(sourceReference.Repository, d, cancellationToken).ConfigureAwait(false);;

                // Stuff that (uncompressed) tarball into the image tar stream
                // TODO uncompress!!
                string layerTarballPath = $"{d.Digest.Substring("sha256:".Length)}/layer.tar";
                await writer.WriteEntryAsync(localPath, layerTarballPath, cancellationToken).ConfigureAwait(false);
                layerTarballPaths.Add(layerTarballPath);
            }
            else
            {
                throw new NotImplementedException(Resource.FormatString(
                    nameof(Strings.MissingLinkToRegistry),
                    d.Digest,
                    sourceReference.Registry?.ToString() ?? "<null>"));
            }
        }

        // add config
        string configTarballPath = $"{image.ImageSha}.json";
        cancellationToken.ThrowIfCancellationRequested();
        using (MemoryStream configStream = new MemoryStream(Encoding.UTF8.GetBytes(image.Config)))
        {
            PaxTarEntry configEntry = new(TarEntryType.RegularFile, configTarballPath)
            {
                DataStream = configStream
            };

            await writer.WriteEntryAsync(configEntry, cancellationToken).ConfigureAwait(false);
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

        cancellationToken.ThrowIfCancellationRequested();
        using (MemoryStream manifestStream = new MemoryStream(Encoding.UTF8.GetBytes(manifestNode.ToJsonString())))
        {
            PaxTarEntry manifestEntry = new(TarEntryType.RegularFile, "manifest.json")
            {
                DataStream = manifestStream
            };

            await writer.WriteEntryAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
        }
    }

}

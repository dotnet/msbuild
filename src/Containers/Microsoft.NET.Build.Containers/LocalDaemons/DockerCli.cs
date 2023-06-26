// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

// Wraps the 'docker'/'podman' cli.
internal sealed class DockerCli : ILocalRegistry
{
    public const string DockerCommand = "docker";
    public const string PodmanCommand = "podman";

    private const string Commands = $"{DockerCommand}/{PodmanCommand}";

    private readonly ILogger _logger;
    private string? _commandPath;

    public DockerCli(string? command, ILoggerFactory logger)
    {
        if (!(command == null ||
              command == PodmanCommand ||
              command == DockerCommand))
        {
            throw new ArgumentException($"{command} is an unknown command.");
        }

        this._commandPath = command;
        this._logger = logger.CreateLogger<DockerCli>();
    }

    public DockerCli(ILoggerFactory loggerFactory) : this(null, loggerFactory)
    { }

    public async Task LoadAsync(BuiltImage image, ImageReference sourceReference, ImageReference destinationReference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? commandPath = await GetCommandPathAsync(cancellationToken);
        if (commandPath is null)
        {
            throw new NotImplementedException(Resource.FormatString(Strings.DockerProcessCreationFailed, Commands));
        }

        // call `docker load` and get it ready to receive input
        ProcessStartInfo loadInfo = new(commandPath, $"load");
        loadInfo.RedirectStandardInput = true;
        loadInfo.RedirectStandardOutput = true;
        loadInfo.RedirectStandardError = true;

        using Process? loadProcess = Process.Start(loadInfo);

        if (loadProcess is null)
        {
            throw new NotImplementedException(Resource.FormatString(Strings.DockerProcessCreationFailed, commandPath));
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

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        bool commandPathWasUnknown = this._commandPath is null; // avoid running the version command twice.
        string? commandPath = await GetCommandPathAsync(cancellationToken);
        if (commandPath is null)
        {
            _logger.LogError($"Cannot find {Commands} executable.");
            return false;
        }

        try
        {
            switch (commandPath)
            {
                case DockerCommand:
                {
                    JsonDocument config = GetConfig();

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
                        _logger.LogError($"The daemon server reported errors: {messages}");
                        return false;
                    }
                }
                case PodmanCommand:
                    return commandPathWasUnknown || await TryRunVersionCommandAsync(PodmanCommand, cancellationToken);
                default:
                    throw new NotImplementedException($"{commandPath} is an unknown command.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(Strings.LocalDocker_FailedToGetConfig, ex.Message);
            _logger.LogTrace("Full information: {0}", ex);
            return false;
        }
    }

    ///<inheritdoc/>
    public bool IsAvailable()
        => IsAvailableAsync(default).GetAwaiter().GetResult();

    public string? GetCommand()
        => GetCommandPathAsync(default).GetAwaiter().GetResult();

    /// <summary>
    /// Gets docker configuration.
    /// </summary>
    /// <param name="sync">when <see langword="true"/>, the method is executed synchronously.</param>
    /// <exception cref="DockerLoadException">when failed to retrieve docker configuration.</exception>
    internal static JsonDocument GetConfig()
    {
        Process proc = new()
        {
            StartInfo = new ProcessStartInfo("docker", "info --format=\"{{json .}}\"")
        };

        try
        {
            Command dockerCommand = new(proc);
            dockerCommand.CaptureStdOut();
            dockerCommand.CaptureStdErr();
            CommandResult dockerCommandResult = dockerCommand.Execute();


            if (dockerCommandResult.ExitCode != 0)
            {
                throw new DockerLoadException(Resource.FormatString(
                    nameof(Strings.DockerInfoFailed),
                    dockerCommandResult.ExitCode,
                    dockerCommandResult.StdOut,
                    dockerCommandResult.StdErr));
            }

            return JsonDocument.Parse(dockerCommandResult.StdOut);


        }
        catch (Exception e) when (e is not DockerLoadException)
        {
            throw new DockerLoadException(Resource.FormatString(nameof(Strings.DockerInfoFailed_Ex), e.Message));
        }
    }

    private static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e) => throw new NotImplementedException();

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
                string localPath = await registry.DownloadBlobAsync(sourceReference.Repository, d, cancellationToken).ConfigureAwait(false); ;

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

    private async ValueTask<string?> GetCommandPathAsync(CancellationToken cancellationToken)
    {
        if (_commandPath != null)
        {
            return _commandPath;
        }

        // Try to find the docker or podman cli.
        // On systems with podman it's not uncommon for docker to be an alias to podman.
        // We try to find podman first so we can identify those systems to be using podman.
        foreach (var command in new[] { PodmanCommand, DockerCommand })
        {
            if (await TryRunVersionCommandAsync(command, cancellationToken))
            {
                _commandPath = command;
                break;
            }
        }

        return _commandPath;
    }

    private async Task<bool> TryRunVersionCommandAsync(string command, CancellationToken cancellationToken)
    {
        try
        {
            ProcessStartInfo psi = new(command, "version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi)!;
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

}

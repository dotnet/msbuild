// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Formats.Tar;
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
    private string? _command;
    private string? _fullCommandPath;

    public DockerCli(string? command, ILoggerFactory loggerFactory)
    {
        if (!(command == null ||
              command == PodmanCommand ||
              command == DockerCommand))
        {
            throw new ArgumentException($"{command} is an unknown command.");
        }

        _command = command;
        _logger = loggerFactory.CreateLogger<DockerCli>();
    }

    public DockerCli(ILoggerFactory loggerFactory) : this(null, loggerFactory)
    { }

    private static string FindFullPathFromPath(string command)
    {
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            string fullPath = Path.Combine(directory, command + FileNameSuffixes.CurrentPlatform.Exe);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return command;
    }

    private async ValueTask<string> FindFullCommandPath(CancellationToken cancellationToken)
    {
        if (_fullCommandPath != null)
        {
            return _fullCommandPath;
        }

        string? command = await GetCommandAsync(cancellationToken);
        if (command is null)
        {
            throw new NotImplementedException(Resource.FormatString(Strings.ContainerRuntimeProcessCreationFailed, Commands));
        }

        _fullCommandPath = FindFullPathFromPath(command);

        return _fullCommandPath;
    }

    public async Task LoadAsync(BuiltImage image, SourceImageReference sourceReference, DestinationImageReference destinationReference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string commandPath = await FindFullCommandPath(cancellationToken);

        // call `docker load` and get it ready to receive input
        ProcessStartInfo loadInfo = new(commandPath, $"load")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process? loadProcess = Process.Start(loadInfo);

        if (loadProcess is null)
        {
            throw new NotImplementedException(Resource.FormatString(Strings.ContainerRuntimeProcessCreationFailed, commandPath));
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
        bool commandPathWasUnknown = _command is null; // avoid running the version command twice.
        string? command = await GetCommandAsync(cancellationToken);
        if (command is null)
        {
            _logger.LogError($"Cannot find {Commands} executable.");
            return false;
        }

        try
        {
            switch (command)
            {
                case DockerCommand:
                    {
                        JsonDocument config = GetDockerConfig();

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
                    throw new NotImplementedException($"{command} is an unknown command.");
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
        => GetCommandAsync(default).GetAwaiter().GetResult();

    /// <summary>
    /// Gets docker configuration.
    /// </summary>
    /// <param name="sync">when <see langword="true"/>, the method is executed synchronously.</param>
    /// <exception cref="DockerLoadException">when failed to retrieve docker configuration.</exception>
    internal static JsonDocument GetDockerConfig()
    {
        string dockerPath = FindFullPathFromPath("docker");
        Process proc = new()
        {
            StartInfo = new ProcessStartInfo(dockerPath, "info --format=\"{{json .}}\"")
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

    public static async Task WriteImageToStreamAsync(BuiltImage image, SourceImageReference sourceReference, DestinationImageReference destinationReference, Stream imageStream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using TarWriter writer = new(imageStream, TarEntryFormat.Pax, leaveOpen: true);


        // Feed each layer tarball into the stream
        JsonArray layerTarballPaths = new();

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
        using (MemoryStream configStream = new(Encoding.UTF8.GetBytes(image.Config)))
        {
            PaxTarEntry configEntry = new(TarEntryType.RegularFile, configTarballPath)
            {
                DataStream = configStream
            };

            await writer.WriteEntryAsync(configEntry, cancellationToken).ConfigureAwait(false);
        }

        // Add manifest
        JsonArray tagsNode = new();
        foreach (string tag in destinationReference.Tags)
        {
            tagsNode.Add($"{destinationReference.Repository}:{tag}");
        }

        JsonNode manifestNode = new JsonArray(new JsonObject
        {
            { "Config", configTarballPath },
            { "RepoTags", tagsNode },
            { "Layers", layerTarballPaths }
        });

        cancellationToken.ThrowIfCancellationRequested();
        using (MemoryStream manifestStream = new(Encoding.UTF8.GetBytes(manifestNode.ToJsonString())))
        {
            PaxTarEntry manifestEntry = new(TarEntryType.RegularFile, "manifest.json")
            {
                DataStream = manifestStream
            };

            await writer.WriteEntryAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<string?> GetCommandAsync(CancellationToken cancellationToken)
    {
        if (_command != null)
        {
            return _command;
        }

        // Try to find the docker or podman cli.
        // On systems with podman it's not uncommon for docker to be an alias to podman.
        // We have to attempt to locate both binaries and inspect the output of the 'docker' binary if present to determine
        // if it is actually podman.
        var podmanCommand = TryRunVersionCommandAsync(PodmanCommand, cancellationToken);
        var dockerCommand = TryRunVersionCommandAsync(DockerCommand, cancellationToken);

        await Task.WhenAll(
            podmanCommand,
            dockerCommand
        ).ConfigureAwait(false);

        // be explicit with this check so that we don't do the link target check unless it might actually be a solution.
        if (dockerCommand.Result && podmanCommand.Result && IsPodmanAlias())
        {
            _command = PodmanCommand;
        }
        else if (dockerCommand.Result)
        {
            _command = DockerCommand;
        }
        else if (podmanCommand.Result)
        {
            _command = PodmanCommand;
        }

        return _command;
    }

    private static bool IsPodmanAlias()
    {
        // If both exist we need to check and see if the docker command is actually docker,
        // or if it is a podman script in a trenchcoat.
        try
        {
            var dockerinfo = GetDockerConfig().RootElement;
            // Docker's info output has a 'DockerRootDir' top-level property string that is a good marker,
            // while Podman has a 'host' top-level property object with a 'buildahVersion' subproperty
            var hasdockerProperty =
                dockerinfo.TryGetProperty("DockerRootDir", out var dockerRootDir) && dockerRootDir.GetString() is not null;
            var hasPodmanProperty = dockerinfo.TryGetProperty("host", out var host) && host.TryGetProperty("buildahVersion", out var buildahVersion) && buildahVersion.GetString() is not null;
            return !hasdockerProperty && hasPodmanProperty;
        }
        catch
        {
            return false;
        }
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

    public override string ToString()
    {
        return string.Format(Strings.DockerCli_PushInfo, _command);
    }
}

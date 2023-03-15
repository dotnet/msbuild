// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

public static class ContainerBuilder
{
    public static async Task ContainerizeAsync(
        DirectoryInfo folder,
        string workingDir,
        string registryName,
        string baseName,
        string baseTag,
        string[] entrypoint,
        string[]? entrypointArgs,
        string imageName,
        string[] imageTags,
        string? outputRegistry,
        Dictionary<string, string> labels,
        Port[]? exposedPorts,
        Dictionary<string, string> envVars,
        string containerRuntimeIdentifier,
        string ridGraphPath,
        string localContainerDaemon,
        string? containerUser,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var isDaemonPull = String.IsNullOrEmpty(registryName);
        if (isDaemonPull)
        {
            throw new NotSupportedException(Resource.GetString(nameof(Strings.DontKnowHowToPullImages)));
        }

        Registry baseRegistry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName));
        ImageReference sourceImageReference = new(baseRegistry, baseName, baseTag);
        var isDockerPush = String.IsNullOrEmpty(outputRegistry);
        var destinationImageReferences = imageTags.Select(t => new ImageReference(isDockerPush ? null : new Registry(ContainerHelpers.TryExpandRegistryToUri(outputRegistry!)), imageName, t));

        ImageBuilder imageBuilder = await baseRegistry.GetImageManifestAsync(baseName, baseTag, containerRuntimeIdentifier, ridGraphPath, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        imageBuilder.SetWorkingDirectory(workingDir);

        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
        };

        Layer l = Layer.FromDirectory(folder.FullName, workingDir, imageBuilder.IsWindows);

        imageBuilder.AddLayer(l);

        imageBuilder.SetEntryPoint(entrypoint, entrypointArgs);

        foreach (KeyValuePair<string, string> label in labels)
        {
            // labels are validated by System.CommandLine API
            imageBuilder.AddLabel(label.Key, label.Value);
        }

        foreach (KeyValuePair<string, string> envVar in envVars)
        {
            imageBuilder.AddEnvironmentVariable(envVar.Key, envVar.Value);
        }

        foreach ((int number, PortType type) in exposedPorts ?? Array.Empty<Port>())
        {
            // ports are validated by System.CommandLine API
            imageBuilder.ExposePort(number, type);
        }

        if (containerUser is { } user)
        {
            imageBuilder.SetUser(user);
        }

        BuiltImage builtImage = imageBuilder.Build();

        cancellationToken.ThrowIfCancellationRequested();

        foreach (var destinationImageReference in destinationImageReferences)
        {
            if (destinationImageReference.Registry is { } outReg)
            {
                try
                {
                    await outReg.PushAsync(
                        builtImage,
                        sourceImageReference,
                        destinationImageReference,
                        (message) => Console.WriteLine($"Containerize: {message}"),
                        cancellationToken).ConfigureAwait(false);
                    Console.WriteLine($"Containerize: Pushed container '{destinationImageReference.RepositoryAndTag}' to registry '{outputRegistry}'");
                }
                catch (Exception e)
                {
                    Console.WriteLine(DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings.RegistryOutputPushFailed), e.Message));
                    Environment.ExitCode = 1;
                }
            }
            else
            {

                var localDaemon = GetLocalDaemon(localContainerDaemon, Console.WriteLine);
                if (!(await localDaemon.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
                {
                    Console.WriteLine(DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings.LocalDaemondNotAvailable)));
                    Environment.ExitCode = 7;
                    return;
                }
                try
                {
                    await localDaemon.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
                    Console.WriteLine("Containerize: Pushed container '{0}' to Docker daemon", destinationImageReference.RepositoryAndTag);
                }
                catch (Exception e)
                {
                    Console.WriteLine(DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings.RegistryOutputPushFailed), e.Message));
                    Environment.ExitCode = 1;
                }
            }
        }
    }

    private static LocalDocker GetLocalDaemon(string localDaemonType, Action<string> logger)
    {
        var daemon = localDaemonType switch
        {
            KnownDaemonTypes.Docker => new LocalDocker(logger),
            _ => throw new ArgumentException(Resource.FormatString(nameof(Strings.UnknownDaemonType), localDaemonType, String.Join(",", KnownDaemonTypes.SupportedLocalDaemonTypes)), nameof(localDaemonType))
        };
        return daemon;
    }
}

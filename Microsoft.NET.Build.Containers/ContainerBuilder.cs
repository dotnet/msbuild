// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public static class ContainerBuilder
{
    private static LocalDocker GetLocalDaemon(string localDaemonType, Action<string> logger) {
        var daemon = localDaemonType switch {
            KnownDaemonTypes.Docker => new LocalDocker(logger),
            _ => throw new ArgumentException($"Unknown local container daemon type '{localDaemonType}'. Valid local container daemon types are {String.Join(",", KnownDaemonTypes.SupportedLocalDaemonTypes)}", nameof(localDaemonType))
        };
        return daemon;
    }
    public static async Task Containerize(DirectoryInfo folder, string workingDir, string registryName, string baseName, string baseTag, string[] entrypoint, string[] entrypointArgs, string imageName, string[] imageTags, string? outputRegistry, string[] labels, Port[] exposedPorts, string[] envVars, string containerRuntimeIdentifier, string ridGraphPath, string localContainerDaemon)
    {
        var isDaemonPull = String.IsNullOrEmpty(registryName);
        if (isDaemonPull)
        {
            throw new NotSupportedException("Don't know how to pull images from local daemons at the moment");
        }

        Registry baseRegistry = new Registry(ContainerHelpers.TryExpandRegistryToUri(registryName));
        ImageReference sourceImageReference = new(baseRegistry, baseName, baseTag);
        var isDockerPush = String.IsNullOrEmpty(outputRegistry);
        var destinationImageReferences = imageTags.Select(t => new ImageReference(isDockerPush ? null : new Registry(ContainerHelpers.TryExpandRegistryToUri(outputRegistry!)), imageName, t));

        var img = await baseRegistry.GetImageManifest(baseName, baseTag, containerRuntimeIdentifier, ridGraphPath).ConfigureAwait(false);

        img.WorkingDirectory = workingDir;

        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
        };

        Layer l = Layer.FromDirectory(folder.FullName, workingDir);

        img.AddLayer(l);

        img.SetEntrypoint(entrypoint, entrypointArgs);

        foreach (var label in labels)
        {
            string[] labelPieces = label.Split('=');

            // labels are validated by System.CommandLine API
            img.Label(labelPieces[0], labelPieces[1]);
        }

        foreach (string envVar in envVars)
        {
            string[] envPieces = envVar.Split('=', 2);

            img.AddEnvironmentVariable(envPieces[0], envPieces[1]);
        }

        foreach (var (number, type) in exposedPorts)
        {
            // ports are validated by System.CommandLine API
            img.ExposePort(number, type);
        }

        foreach (var destinationImageReference in destinationImageReferences)
        {
            if (destinationImageReference.Registry is { } outReg)
            {
                try
                {
                    outReg.Push(img, sourceImageReference, destinationImageReference, (message) => Console.WriteLine($"Containerize: {message}")).Wait();
                    Console.WriteLine($"Containerize: Pushed container '{destinationImageReference.RepositoryAndTag}' to registry '{outputRegistry}'");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Containerize: error CONTAINER001: Failed to push to output registry: {e.Message}");
                    Environment.ExitCode = 1;
                }
            }
            else
            {

                var localDaemon = GetLocalDaemon(localContainerDaemon, Console.WriteLine);
                if (!(await localDaemon.IsAvailable().ConfigureAwait(false)))
                {
                    Console.WriteLine("Containerize: error CONTAINER007: The Docker daemon is not available, but pushing to a local daemon was requested. Please start Docker and try again.");
                    Environment.ExitCode = 7;
                    return;
                }
                try
                {
                    localDaemon.Load(img, sourceImageReference, destinationImageReference).Wait();
                    Console.WriteLine("Containerize: Pushed container '{0}' to Docker daemon", destinationImageReference.RepositoryAndTag);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Containerize: error CONTAINER001: Failed to push to local docker registry: {e.Message}");
                    Environment.ExitCode = 1;
                }
            }
        }
    }
}

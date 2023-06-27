// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

public static class ContainerBuilder
{
    public static async Task<int> ContainerizeAsync(
        DirectoryInfo publishDirectory,
        string workingDir,
        string baseRegistry,
        string baseImageName,
        string baseImageTag,
        string[] entrypoint,
        string[]? cmd,
        string imageName,
        string[] imageTags,
        string? outputRegistry,
        Dictionary<string, string> labels,
        Port[]? exposedPorts,
        Dictionary<string, string> envVars,
        string containerRuntimeIdentifier,
        string ridGraphPath,
        string localRegistry,
        string? containerUser,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!publishDirectory.Exists)
        {
            throw new ArgumentException(string.Format(Resource.GetString(nameof(Strings.PublishDirectoryDoesntExist)), nameof(publishDirectory), publishDirectory.FullName));
        }
        ILogger logger = loggerFactory.CreateLogger("Containerize");
        logger.LogTrace("Trace logging: enabled.");

        bool isLocalPull = string.IsNullOrEmpty(baseRegistry);
        Registry? sourceRegistry = isLocalPull ? null : new Registry(ContainerHelpers.TryExpandRegistryToUri(baseRegistry), logger);
        ImageReference sourceImageReference = new(sourceRegistry, baseImageName, baseImageTag);

        bool isLocalPush = string.IsNullOrEmpty(outputRegistry);
        Registry? destinationRegistry = isLocalPush ? null : new Registry(ContainerHelpers.TryExpandRegistryToUri(outputRegistry!), logger);
        IEnumerable<ImageReference> destinationImageReferences = imageTags.Select(t => new ImageReference(destinationRegistry, imageName, t));

        ImageBuilder? imageBuilder;
        if (sourceRegistry is { } registry)
        {
            imageBuilder = await registry.GetImageManifestAsync(
                baseImageName,
                baseImageTag,
                containerRuntimeIdentifier,
                ridGraphPath,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new NotSupportedException(Resource.GetString(nameof(Strings.ImagePullNotSupported)));
        }
        if (imageBuilder is null)
        {
            Console.WriteLine(Resource.GetString(nameof(Strings.BaseImageNotFound)), sourceImageReference, containerRuntimeIdentifier);
            return 1;
        }
        logger.LogInformation(Strings.ContainerBuilder_StartBuildingImage, imageName, string.Join(",", imageName), sourceImageReference);
        cancellationToken.ThrowIfCancellationRequested();

        Layer newLayer = Layer.FromDirectory(publishDirectory.FullName, workingDir, imageBuilder.IsWindows);
        imageBuilder.AddLayer(newLayer);
        imageBuilder.SetWorkingDirectory(workingDir);
        imageBuilder.SetEntrypointAndCmd(entrypoint, cmd ?? Array.Empty<string>());
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
        if (containerUser is { Length: > 0 } user)
        {
            imageBuilder.SetUser(user);
        }
        BuiltImage builtImage = imageBuilder.Build();
        cancellationToken.ThrowIfCancellationRequested();

        foreach (ImageReference destinationImageReference in destinationImageReferences)
        {
            if (isLocalPush)
            {
                ILocalRegistry containerRegistry = KnownLocalRegistryTypes.CreateLocalRegistry(localRegistry, loggerFactory);
                if (!(await containerRegistry.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
                {
                    Console.WriteLine(DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings.LocalRegistryNotAvailable)));
                    return 7;
                }

                try
                {
                    await containerRegistry.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
                    logger.LogInformation(Strings.ContainerBuilder_ImageUploadedToLocalDaemon, destinationImageReference.RepositoryAndTag);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings.RegistryOutputPushFailed), ex.Message));
                    return 1;
                }
            }
            else
            {
                try
                {
                    if (destinationImageReference.Registry is not null)
                    {
                        await (destinationImageReference.Registry.PushAsync(
                            builtImage,
                            sourceImageReference,
                            destinationImageReference,
                            message => Console.WriteLine($"Containerize: {message}"),
                            cancellationToken)).ConfigureAwait(false);
                        logger.LogInformation(Strings.ContainerBuilder_ImageUploadedToRegistry, destinationImageReference.RepositoryAndTag, destinationImageReference.Registry.RegistryName);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(DiagnosticMessage.ErrorFromResourceWithCode(nameof(Strings.RegistryOutputPushFailed), e.Message));
                    return 1;
                }
            }
        }
        return 0;
    }
}

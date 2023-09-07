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
        string[] entrypointArgs,
        string[] defaultArgs,
        string[] appCommand,
        string[] appCommandArgs,
        string appCommandInstruction,
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
        string? archiveOutputPath,
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
        Registry? sourceRegistry = isLocalPull ? null : new Registry(baseRegistry, logger);
        SourceImageReference sourceImageReference = new(sourceRegistry, baseImageName, baseImageTag);

        DestinationImageReference destinationImageReference = DestinationImageReference.CreateFromSettings(
            imageName,
            imageTags,
            loggerFactory,
            archiveOutputPath,
            outputRegistry,
            localRegistry);

        ImageBuilder? imageBuilder;
        if (sourceRegistry is { } registry)
        {
            try
            {
                imageBuilder = await registry.GetImageManifestAsync(
                    baseImageName,
                    baseImageTag,
                    containerRuntimeIdentifier,
                    ridGraphPath,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (RepositoryNotFoundException)
            {
                logger.LogError(Resource.FormatString(nameof(Strings.RepositoryNotFound), baseImageName, baseImageTag, registry.RegistryName));
                return 1;
            }
            catch (UnableToAccessRepositoryException)
            {
                logger.LogError(Resource.FormatString(nameof(Strings.UnableToAccessRepository), baseImageName, registry.RegistryName));
                return 1;
            }
            catch (ContainerHttpException e)
            {
                logger.LogError(e.Message);
                return 1;
            }
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

        bool hasErrors = false;
        (string[] imageEntrypoint, string[] imageCmd) = ImageBuilder.DetermineEntrypointAndCmd(entrypoint, entrypointArgs, defaultArgs, appCommand, appCommandArgs, appCommandInstruction,
            baseImageEntrypoint: imageBuilder.BaseImageConfig.GetEntrypoint(),
            logWarning: s =>
            {
                logger.LogWarning(Resource.GetString(nameof(s)));
            },
            logError: (s, a) =>
            {
                hasErrors = true;
                if (a is null)
                {
                    logger.LogError(Resource.GetString(nameof(s)));
                }
                else
                {
                    logger.LogError(Resource.FormatString(nameof(s), a));
                }
            });
        if (hasErrors)
        {
            return 1;
        }
        imageBuilder.SetEntrypointAndCmd(imageEntrypoint, imageCmd);

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

        int exitCode;
        switch (destinationImageReference.Kind)
        {
            case DestinationImageReferenceKind.LocalRegistry:
                exitCode = await PushToLocalRegistryAsync(
                    logger,
                    builtImage,
                    sourceImageReference,
                    destinationImageReference,
                    cancellationToken).ConfigureAwait(false);
                break;
            case DestinationImageReferenceKind.RemoteRegistry:
                exitCode = await PushToRemoteRegistryAsync(
                    logger,
                    builtImage,
                    sourceImageReference,
                    destinationImageReference,
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return exitCode;
    }

    private static async Task<int> PushToLocalRegistryAsync(ILogger logger, BuiltImage builtImage, SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        CancellationToken cancellationToken)
    {
        ILocalRegistry containerRegistry = destinationImageReference.LocalRegistry!;
        if (!(await containerRegistry.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
        {
            logger.LogError(Resource.FormatString(nameof(Strings.LocalRegistryNotAvailable)));
            return 7;
        }

        try
        {
            await containerRegistry.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(Strings.ContainerBuilder_ImageUploadedToLocalDaemon, destinationImageReference, containerRegistry);
        }
        catch (Exception ex)
        {
            logger.LogError(Resource.FormatString(nameof(Strings.RegistryOutputPushFailed), ex.Message));
            return 1;
        }

        return 0;
    }

    private static async Task<int> PushToRemoteRegistryAsync(ILogger logger, BuiltImage builtImage, SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        CancellationToken cancellationToken)
    {
        try
        {
            await (destinationImageReference.RemoteRegistry!.PushAsync(
                builtImage,
                sourceImageReference,
                destinationImageReference,
                cancellationToken)).ConfigureAwait(false);
            logger.LogInformation(Strings.ContainerBuilder_ImageUploadedToRegistry, destinationImageReference, destinationImageReference.RemoteRegistry.RegistryName);
        }
        catch (UnableToAccessRepositoryException)
        {
            logger.LogError(Resource.FormatString(nameof(Strings.UnableToAccessRepository), destinationImageReference.Repository, destinationImageReference.RemoteRegistry!.RegistryName));
            return 1;
        }
        catch (Exception e)
        {
            logger.LogError(Resource.FormatString(nameof(Strings.RegistryOutputPushFailed), e.Message));
            return 1;
        }

        return 0;
    }
}

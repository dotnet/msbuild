// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.LocalDaemons;
using Microsoft.NET.Build.Containers.Logging;
using Microsoft.NET.Build.Containers.Resources;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateNewImage : Microsoft.Build.Utilities.Task, ICancelableTask, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolExe { get; set; }

    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolPath { get; set; }

    private bool IsLocalPull => string.IsNullOrEmpty(BaseRegistry);

    public void Cancel() => _cancellationTokenSource.Cancel();

    public override bool Execute()
    {
        return Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token)).GetAwaiter().GetResult();
    }

    internal async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateNewImage>();

        if (!Directory.Exists(PublishDirectory))
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.PublishDirectoryDoesntExist), nameof(PublishDirectory), PublishDirectory);
            return !Log.HasLoggedErrors;
        }

        Registry? sourceRegistry = IsLocalPull ? null : new Registry(BaseRegistry, logger);
        SourceImageReference sourceImageReference = new(sourceRegistry, BaseImageName, BaseImageTag);

        DestinationImageReference destinationImageReference = DestinationImageReference.CreateFromSettings(
            Repository,
            ImageTags,
            msbuildLoggerFactory,
            ArchiveOutputPath,
            OutputRegistry,
            LocalRegistry);

        ImageBuilder? imageBuilder;
        if (sourceRegistry is { } registry)
        {
            try
            {
                imageBuilder = await registry.GetImageManifestAsync(
                    BaseImageName,
                    BaseImageTag,
                    ContainerRuntimeIdentifier,
                    RuntimeIdentifierGraphPath,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (RepositoryNotFoundException)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.RepositoryNotFound), BaseImageName, BaseImageTag, registry.RegistryName);
                return !Log.HasLoggedErrors;
            }
            catch (UnableToAccessRepositoryException)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.UnableToAccessRepository), BaseImageName, registry.RegistryName);
                return !Log.HasLoggedErrors;
            }
            catch (ContainerHttpException e)
            {
                Log.LogErrorFromException(e, showStackTrace: false, showDetail: true, file: null);
                return !Log.HasLoggedErrors;
            }
        }
        else
        {
            throw new NotSupportedException(Resource.GetString(nameof(Strings.ImagePullNotSupported)));
        }

        if (imageBuilder is null)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.BaseImageNotFound), sourceImageReference, ContainerRuntimeIdentifier);
            return !Log.HasLoggedErrors;
        }

        SafeLog(Strings.ContainerBuilder_StartBuildingImage, Repository, String.Join(",", ImageTags), sourceImageReference);

        Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory, imageBuilder.IsWindows);
        imageBuilder.AddLayer(newLayer);
        imageBuilder.SetWorkingDirectory(WorkingDirectory);

        (string[] entrypoint, string[] cmd) = DetermineEntrypointAndCmd(baseImageEntrypoint: imageBuilder.BaseImageConfig.GetEntrypoint());
        imageBuilder.SetEntrypointAndCmd(entrypoint, cmd);

        foreach (ITaskItem label in Labels)
        {
            imageBuilder.AddLabel(label.ItemSpec, label.GetMetadata("Value"));
        }

        SetEnvironmentVariables(imageBuilder, ContainerEnvironmentVariables);

        SetPorts(imageBuilder, ExposedPorts);

        if (ContainerUser is { Length: > 0 } user)
        {
            imageBuilder.SetUser(user);
        }

        // at the end of this step, if any failed then bail out.
        if (Log.HasLoggedErrors)
        {
            return false;
        }

        BuiltImage builtImage = imageBuilder.Build();
        cancellationToken.ThrowIfCancellationRequested();

        // at this point we're done with modifications and are just pushing the data other places
        GeneratedContainerManifest = JsonSerializer.Serialize(builtImage.Manifest);
        GeneratedContainerConfiguration = builtImage.Config;
        GeneratedContainerDigest = builtImage.Manifest.GetDigest();
        GeneratedArchiveOutputPath = ArchiveOutputPath;

        switch (destinationImageReference.Kind)
        {
            case DestinationImageReferenceKind.LocalRegistry:
                await PushToLocalRegistryAsync(builtImage,
                    sourceImageReference,
                    destinationImageReference,
                    cancellationToken).ConfigureAwait(false);
                break;
            case DestinationImageReferenceKind.RemoteRegistry:
                await PushToRemoteRegistryAsync(builtImage,
                    sourceImageReference,
                    destinationImageReference,
                    cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return !Log.HasLoggedErrors;
    }

    private async Task PushToLocalRegistryAsync(BuiltImage builtImage, SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        CancellationToken cancellationToken)
    {
        ILocalRegistry localRegistry = destinationImageReference.LocalRegistry!;
        if (!(await localRegistry.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.LocalRegistryNotAvailable));
            return;
        }
        try
        {
            await localRegistry.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
            SafeLog(Strings.ContainerBuilder_ImageUploadedToLocalDaemon, destinationImageReference, localRegistry);

            if (localRegistry is ArchiveFileRegistry archive)
            {
                GeneratedArchiveOutputPath = archive.ArchiveOutputPath;
            }
        }
        catch (ContainerHttpException e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }
        }
        catch (AggregateException ex) when (ex.InnerException is DockerLoadException dle)
        {
            Log.LogErrorFromException(dle, showStackTrace: false);
        }
    }

    private async Task PushToRemoteRegistryAsync(BuiltImage builtImage, SourceImageReference sourceImageReference,
        DestinationImageReference destinationImageReference,
        CancellationToken cancellationToken)
    {
        try
        {
            await destinationImageReference.RemoteRegistry!.PushAsync(
                builtImage,
                sourceImageReference,
                destinationImageReference,
                cancellationToken).ConfigureAwait(false);
            SafeLog(Strings.ContainerBuilder_ImageUploadedToRegistry, destinationImageReference, OutputRegistry);
        }
        catch (UnableToAccessRepositoryException)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.UnableToAccessRepository), destinationImageReference.Repository, destinationImageReference.RemoteRegistry!.RegistryName);
            }
        }
        catch (ContainerHttpException e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }
        }
        catch (Exception e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.RegistryOutputPushFailed), e.Message);
                Log.LogMessage(MessageImportance.Low, "Details: {0}", e);
            }
        }
    }

    private void SetPorts(ImageBuilder image, ITaskItem[] exposedPorts)
    {
        foreach (var port in exposedPorts)
        {
            var portNo = port.ItemSpec;
            var portType = port.GetMetadata("Type");
            if (ContainerHelpers.TryParsePort(portNo, portType, out Port? parsedPort, out ContainerHelpers.ParsePortError? errors))
            {
                image.ExposePort(parsedPort.Value.Number, parsedPort.Value.Type);
            }
            else
            {
                ContainerHelpers.ParsePortError parsedErrors = (ContainerHelpers.ParsePortError)errors!;

                if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.MissingPortNumber))
                {
                    Log.LogErrorWithCodeFromResources(nameof(Strings.MissingPortNumber), port.ItemSpec);
                }
                else
                {
                    if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber) && parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortType))
                    {
                        Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidPort_NumberAndType), portNo, portType);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidPort_Number), portNo);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortType))
                    {
                        Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidPort_Type), portType);
                    }
                }
            }
        }
    }

    private void SetEnvironmentVariables(ImageBuilder img, ITaskItem[] envVars)
    {
        foreach (ITaskItem envVar in envVars)
        {
            var value = envVar.GetMetadata("Value");
            img.AddEnvironmentVariable(envVar.ItemSpec, value);
        }
    }

    private void SafeLog(string message, params object[] formatParams)
    {
        if (BuildEngine != null) Log.LogMessage(MessageImportance.High, message, formatParams);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
    }

    internal (string[] entrypoint, string[] cmd) DetermineEntrypointAndCmd(string[]? baseImageEntrypoint)
    {
        string[] entrypoint = Entrypoint.Select(i => i.ItemSpec).ToArray();
        string[] entrypointArgs = EntrypointArgs.Select(i => i.ItemSpec).ToArray();
        string[] cmd = DefaultArgs.Select(i => i.ItemSpec).ToArray();
        string[] appCommand = AppCommand.Select(i => i.ItemSpec).ToArray();
        string[] appCommandArgs = AppCommandArgs.Select(i => i.ItemSpec).ToArray();
        string appCommandInstruction = AppCommandInstruction;

        return ImageBuilder.DetermineEntrypointAndCmd(entrypoint, entrypointArgs, cmd, appCommand, appCommandArgs, appCommandInstruction, baseImageEntrypoint,
            logWarning: s => Log.LogWarningWithCodeFromResources(s),
            logError: (s, a) => { if (a is null) Log.LogErrorWithCodeFromResources(s); else Log.LogErrorWithCodeFromResources(s, a); });
    }
}

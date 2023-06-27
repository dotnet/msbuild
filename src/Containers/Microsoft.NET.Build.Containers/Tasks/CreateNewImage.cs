// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
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

    private bool IsLocalPush => string.IsNullOrEmpty(OutputRegistry);

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

        Registry? sourceRegistry = IsLocalPull ? null : new Registry(ContainerHelpers.TryExpandRegistryToUri(BaseRegistry), logger);
        ImageReference sourceImageReference = new(sourceRegistry, BaseImageName, BaseImageTag);

        Registry? destinationRegistry = IsLocalPush ? null : new Registry(ContainerHelpers.TryExpandRegistryToUri(OutputRegistry), logger);
        IEnumerable<ImageReference> destinationImageReferences = ImageTags.Select(t => new ImageReference(destinationRegistry, Repository, t));

        ImageBuilder? imageBuilder;
        if (sourceRegistry is { } registry)
        {
            imageBuilder = await registry.GetImageManifestAsync(
                BaseImageName,
                BaseImageTag,
                ContainerRuntimeIdentifier,
                RuntimeIdentifierGraphPath,
                cancellationToken).ConfigureAwait(false);
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

        SafeLog("Building image '{0}' with tags {1} on top of base image {2}", Repository, String.Join(",", ImageTags), sourceImageReference);

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

        foreach (ImageReference destinationImageReference in destinationImageReferences)
        {
            if (IsLocalPush)
            {
                ILocalRegistry localRegistry = KnownLocalRegistryTypes.CreateLocalRegistry(LocalRegistry, msbuildLoggerFactory);
                if (!(await localRegistry.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
                {
                    Log.LogErrorWithCodeFromResources(nameof(Strings.LocalRegistryNotAvailable));
                    return false;
                }
                try
                {
                    await localRegistry.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
                    SafeLog("Pushed image '{0}' to local registry", destinationImageReference.RepositoryAndTag);
                }
                catch (AggregateException ex) when (ex.InnerException is DockerLoadException dle)
                {
                    Log.LogErrorFromException(dle, showStackTrace: false);
                }
            }
            else
            {
                try
                {
                    if (destinationImageReference.Registry is not null)
                    {
                        await destinationImageReference.Registry.PushAsync(
                            builtImage,
                            sourceImageReference,
                            destinationImageReference,
                            message => SafeLog(message),
                            cancellationToken).ConfigureAwait(false);
                        SafeLog("Pushed image '{0}' to registry '{1}'", destinationImageReference, OutputRegistry);
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
        }

        return !Log.HasLoggedErrors;
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

    private static void SetEnvironmentVariables(ImageBuilder img, ITaskItem[] envVars)
    {
        foreach (ITaskItem envVar in envVars)
        {
            img.AddEnvironmentVariable(envVar.ItemSpec, envVar.GetMetadata("Value"));
        }
    }

    private void SafeLog(string message, params object[] formatParams) {
        if(BuildEngine != null) Log.LogMessage(MessageImportance.High, message, formatParams);
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

        bool setsEntrypoint = entrypoint.Length > 0 || entrypointArgs.Length > 0;
        bool setsCmd = cmd.Length > 0;

        baseImageEntrypoint ??= Array.Empty<string>();
        // Some (Microsoft) base images set 'dotnet' as the ENTRYPOINT. We mustn't use it.
        if (baseImageEntrypoint.Length == 1 && (baseImageEntrypoint[0] == "dotnet" || baseImageEntrypoint[0] == "/usr/bin/dotnet"))
        {
            baseImageEntrypoint = Array.Empty<string>();
        }

        if (string.IsNullOrEmpty(appCommandInstruction))
        {
            if (setsEntrypoint)
            {
                // Backwards-compatibility: before 'AppCommand'/'Cmd' was added, only 'Entrypoint' was available.
                if (!setsCmd && appCommandArgs.Length == 0 && entrypoint.Length == 0)
                {
                    // Copy over the values for starting the application from AppCommand.
                    entrypoint = appCommand;
                    appCommand = Array.Empty<string>();

                    // Use EntrypointArgs as cmd.
                    cmd = entrypointArgs;
                    entrypointArgs = Array.Empty<string>();

                    if (entrypointArgs.Length > 0)
                    {
                        // Log warning: Instead of ContainerEntrypointArgs, use ContainerAppCommandArgs for arguments that must always be set, or ContainerDefaultArgs for default arguments that the user override when creating the container.
                        Log.LogWarningWithCodeFromResources(nameof(Strings.EntrypointArgsSetPreferAppCommandArgs));
                    }

                    appCommandInstruction = KnownAppCommandInstructions.None;
                }
                else
                {
                    // There's an Entrypoint. Use DefaultArgs for the AppCommand.
                    appCommandInstruction = KnownAppCommandInstructions.DefaultArgs;
                }
            }
            else
            {
                // Default to use an Entrypoint.
                // If the base image defines an ENTRYPOINT, print a warning.
                if (baseImageEntrypoint.Length > 0)
                {
                    Log.LogWarningWithCodeFromResources(nameof(Strings.BaseEntrypointOverwritten));
                }
                appCommandInstruction = KnownAppCommandInstructions.Entrypoint;
            }
        }

        if (entrypointArgs.Length > 0 && entrypoint.Length == 0)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.EntrypointArgsSetNoEntrypoint));
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        if (appCommandArgs.Length > 0 && appCommand.Length == 0)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.AppCommandArgsSetNoAppCommand));
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        switch (appCommandInstruction)
        {
            case KnownAppCommandInstructions.None:
                if (appCommand.Length > 0 || appCommandArgs.Length > 0)
                {
                    Log.LogErrorWithCodeFromResources(nameof(Strings.AppCommandSetNotUsed), appCommandInstruction);
                    return (Array.Empty<string>(), Array.Empty<string>());
                }
                break;
            case KnownAppCommandInstructions.DefaultArgs:
                cmd = appCommand.Concat(appCommandArgs).Concat(cmd).ToArray();
                break;
            case KnownAppCommandInstructions.Entrypoint:
                if (setsEntrypoint)
                {
                    Log.LogErrorWithCodeFromResources(nameof(Strings.EntrypointConflictAppCommand), appCommandInstruction);
                    return (Array.Empty<string>(), Array.Empty<string>());
                }
                entrypoint = appCommand;
                entrypointArgs = appCommandArgs;
                break;
            default:
                throw new NotSupportedException(
                    Resource.FormatString(
                        nameof(Strings.UnknownAppCommandInstruction),
                        appCommandInstruction,
                        string.Join(",", KnownAppCommandInstructions.SupportedAppCommandInstructions)));
        }

        return (entrypoint.Length > 0 ? entrypoint.Concat(entrypointArgs).ToArray() : baseImageEntrypoint, cmd);
    }
}

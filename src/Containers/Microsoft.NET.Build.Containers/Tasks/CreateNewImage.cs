// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

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
        if (!Directory.Exists(PublishDirectory))
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.PublishDirectoryDoesntExist), nameof(PublishDirectory), PublishDirectory);
            return !Log.HasLoggedErrors;
        }
        ImageReference sourceImageReference = new(SourceRegistry.Value, BaseImageName, BaseImageTag);
        var destinationImageReferences = ImageTags.Select(t => new ImageReference(DestinationRegistry.Value, ImageName, t));

        ImageBuilder? imageBuilder;
        if (SourceRegistry.Value is { } registry)
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
            Log.LogErrorWithCodeFromResources(nameof(Strings.BaseImageNotFound), sourceImageReference.RepositoryAndTag, ContainerRuntimeIdentifier);
            return !Log.HasLoggedErrors;
        }

        (string[]? entrypoint, string[] cmd) = DetermineEntrypointAndCmd(
            Entrypoint.Select(i => i.ItemSpec).ToArray(), EntrypointArgs.Select(i => i.ItemSpec).ToArray(),
            Cmd.Select(i => i.ItemSpec).ToArray(),
            AppCommand.Select(i => i.ItemSpec).ToArray(), AppCommandArgs.Select(i => i.ItemSpec).ToArray(), AppCommandInstruction,
            baseImageEntrypoint: imageBuilder.BaseImageConfig.GetEntrypoint()
        );

        SafeLog("Building image '{0}' with tags {1} on top of base image {2}", ImageName, String.Join(",", ImageTags), sourceImageReference);

        Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory, imageBuilder.IsWindows);
        imageBuilder.AddLayer(newLayer);
        imageBuilder.SetWorkingDirectory(WorkingDirectory);
        if (entrypoint is not null)
        {
            imageBuilder.SetEntrypointAndCmd(entrypoint, cmd);
        }
        else
        {
            imageBuilder.SetCmd(cmd);
        }

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
                ILocalRegistry localRegistry = GetLocalRegistry(msg => Log.LogMessage(msg));
                if (!(await localRegistry.IsAvailableAsync(cancellationToken).ConfigureAwait(false)))
                {
                    Log.LogErrorWithCodeFromResources(nameof(Strings.LocalRegistryNotAvailable));
                    return false;
                }
                try
                {
                    await localRegistry.LoadAsync(builtImage, sourceImageReference, destinationImageReference, cancellationToken).ConfigureAwait(false);
                    SafeLog("Pushed container '{0}' to local registry", destinationImageReference.RepositoryAndTag);
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
                        SafeLog("Pushed container '{0}' to registry '{1}'", destinationImageReference.RepositoryAndTag, OutputRegistry);
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

    private ILocalRegistry GetLocalRegistry(Action<string> logger) {
        var registry = LocalRegistry switch {
            KnownLocalRegistryTypes.Docker => new DockerCli(logger),
            _ => throw new NotSupportedException(
                Resource.FormatString(
                    nameof(Strings.UnknownLocalRegistryType),
                    LocalRegistry,
                    string.Join(",", KnownLocalRegistryTypes.SupportedLocalRegistryTypes)))
        };
        return registry;
    }

    private Lazy<Registry?> SourceRegistry
    {
        get {
            if(IsLocalPull) {
                return new Lazy<Registry?>(() => null);
            } else {
                return new Lazy<Registry?>(() => new Registry(ContainerHelpers.TryExpandRegistryToUri(BaseRegistry)));
            }
        }
    }

    private Lazy<Registry?> DestinationRegistry {
        get {
            if(IsLocalPush) {
                return new Lazy<Registry?>(() => null);
            } else {
                return new Lazy<Registry?>(() => new Registry(ContainerHelpers.TryExpandRegistryToUri(OutputRegistry)));
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

    static internal (string[]? entryPoint, string[] cmd) DetermineEntrypointAndCmd(
        string[] entryPoint, string[] entryPointArgs,
        string[] cmd,
        string[] appCommand, string[] appCommandArgs, string appCommandInstruction,
        string[]? baseImageEntrypoint)
    {
        bool setsEntrypoint = entryPoint.Length > 0;
        bool setsCmd = cmd.Length > 0;

        if (string.IsNullOrEmpty(appCommandInstruction))
        {
            if (setsCmd && setsEntrypoint)
            {
                // Both are specified, don't emit the AppCommand.
                appCommandInstruction = KnownAppCommandInstructions.None;
            }
            else if (setsCmd)
            {
                // error: A ContainerCmd was specified. 'ContainerAppCommandInstruction' must be set to 'Entrypoint' to add an entrypoint that starts the application, or 'None' use the base image entrypoint (if set).
            }
            else if (setsEntrypoint)
            {
                // Backwards-compatibility: before 'AppCommand'/'Cmd' was added, only 'Entrypoint' was available.
                if (appCommandArgs.Length == 0)
                {
                    // When Entrypoint is empty, it was initialized to the proper value for starting the application.
                    // AppCommand is initialized to that value. Swap the properties.
                    if (entryPoint.Length == 0)
                    {
                        entryPoint = appCommand;
                        appCommand = Array.Empty<string>();
                    }

                    // Use EntrypointArgs as cmd.
                    cmd = entryPointArgs;
                    entryPointArgs = Array.Empty<string>();

                    if (entryPoint.Length == 0 && entryPointArgs.Length > 0)
                    {
                        // Log warning: Instead of ContainerEntrypointArgs, use ContainerAppCommandArgs for arguments that must always be set, or ContainerCmd for default arguments that the user override when creating the container.
                    }

                    appCommandInstruction = KnownAppCommandInstructions.None;
                }
                else
                {
                    // error: You have specified a ContainerEntrypoint with ContainerAppCommandArgs. Set 'ContainerAppCommandInstruction' to 'Cmd' to add the ContainerAppCommand that starts the application.
                    //        Alternatively, use 'ContainerEntrypointArgs' or 'ContainerCmd' to specify the arguments instead of 'ContainerAppCommandArgs'.
                    appCommandInstruction = KnownAppCommandInstructions.Cmd;
                }
            }
            else
            {
                bool baseImageHasEntrypoint = baseImageEntrypoint?.Length > 0;
                bool entryPointIsDotnet = baseImageEntrypoint?.Length == 1 && (baseImageEntrypoint[0] == "dotnet" || baseImageEntrypoint[0] == "/usr/bin/dotnet");
                bool preserveEntrypoint = baseImageHasEntrypoint && !entryPointIsDotnet;
                appCommandInstruction = preserveEntrypoint ? KnownAppCommandInstructions.Cmd : KnownAppCommandInstructions.Entrypoint;
            }
        }

        if (entryPointArgs.Length > 0 && entryPoint.Length == 0)
        {
            // error: EntryPointArgs can not be set without setting an EntryPoint.
        }

        if (appCommandArgs.Length > 0 && appCommand.Length == 0)
        {
            // error: AppCommandArgs can not be set without setting an AppCommand.
        }

        switch (appCommandInstruction)
        {
            case KnownAppCommandInstructions.None:
                if (appCommandArgs.Length > 0)
                {
                    // error: AppCommandArgs are specified but not used.
                }
                break;
            case KnownAppCommandInstructions.Cmd:
                if (setsCmd)
                {
                    // error: ContainerCmd and AppCommandInstruction = 'Cmd' cannot be combined.
                }
                cmd = appCommand.Concat(appCommandArgs).ToArray();
                break;
            case KnownAppCommandInstructions.Entrypoint:
                if (setsEntrypoint)
                {
                    // error: ContainerEntrypoint and AppCommandInstruction = 'Entrypoint' cannot be combined.
                }
                entryPoint = appCommand;
                entryPointArgs = appCommandArgs;
                break;
            default:
                throw new NotSupportedException(
                    Resource.FormatString(
                        nameof(Strings.UnknownAppCommandInstruction),
                        appCommandInstruction,
                        string.Join(",", KnownAppCommandInstructions.SupportedAppCommandInstructions)));
        }

        return (entryPoint.Length > 0 ? entryPoint.Concat(entryPointArgs).ToArray() : null /* base image entrypoint */,
                cmd);
    }
}

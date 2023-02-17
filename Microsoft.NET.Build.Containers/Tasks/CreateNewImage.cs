// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateNewImage : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolExe { get; set; }

    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolPath { get; set; }

    private bool IsDaemonPush => string.IsNullOrEmpty(OutputRegistry);

    private bool IsDaemonPull => string.IsNullOrEmpty(BaseRegistry);

    public override bool Execute()
    {
        if (!Directory.Exists(PublishDirectory))
        {
            Log.LogError("{0} '{1}' does not exist", nameof(PublishDirectory), PublishDirectory);
            return !Log.HasLoggedErrors;
        }
        ImageReference sourceImageReference = new(SourceRegistry.Value, BaseImageName, BaseImageTag);
        var destinationImageReferences = ImageTags.Select(t => new ImageReference(DestinationRegistry.Value, ImageName, t));

        ImageBuilder imageBuilder = GetBaseImage();

        if (imageBuilder is null)
        {
            Log.LogError($"Couldn't find matching base image for {0} that matches RuntimeIdentifier {1}", sourceImageReference.RepositoryAndTag, ContainerRuntimeIdentifier);
            return !Log.HasLoggedErrors;
        }

        SafeLog("Building image '{0}' with tags {1} on top of base image {2}", ImageName, String.Join(",", ImageTags), sourceImageReference);

        Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory);
        imageBuilder.AddLayer(newLayer);
        imageBuilder.SetWorkingDirectory(WorkingDirectory);
        imageBuilder.SetEntryPoint(Entrypoint.Select(i => i.ItemSpec).ToArray(), EntrypointArgs.Select(i => i.ItemSpec).ToArray());

        foreach (var label in Labels)
        {
            imageBuilder.AddLabel(label.ItemSpec, label.GetMetadata("Value"));
        }

        SetEnvironmentVariables(imageBuilder, ContainerEnvironmentVariables);

        SetPorts(imageBuilder, ExposedPorts);

        // at the end of this step, if any failed then bail out.
        if (Log.HasLoggedErrors)
        {
            return false;
        }

        BuiltImage builtImage = imageBuilder.Build();

        // at this point we're done with modifications and are just pushing the data other places
        GeneratedContainerManifest = JsonSerializer.Serialize(builtImage.Manifest);
        GeneratedContainerConfiguration = builtImage.Config;

        foreach (var destinationImageReference in destinationImageReferences)
        {
            if (IsDaemonPush)
            {
                var localDaemon = GetLocalDaemon(msg => Log.LogMessage(msg));
                if (!localDaemon.IsAvailable().GetAwaiter().GetResult())
                {
                    Log.LogError("The local daemon is not available, but pushing to a local daemon was requested. Please start the daemon and try again.");
                    return false;
                }
                try
                {
                    localDaemon.Load(builtImage, sourceImageReference, destinationImageReference).Wait();
                    SafeLog("Pushed container '{0}' to local daemon", destinationImageReference.RepositoryAndTag);
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
                    destinationImageReference.Registry?.Push(builtImage, sourceImageReference, destinationImageReference, message => SafeLog(message)).Wait();
                    SafeLog("Pushed container '{0}' to registry '{2}'", destinationImageReference.RepositoryAndTag, OutputRegistry);
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
                        Log.LogError("Failed to push to the output registry: {0}", e);
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
                var portString = portType == null ? portNo : $"{portNo}/{portType}";
                if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.MissingPortNumber))
                {
                    Log.LogError("ContainerPort item '{0}' does not specify the port number. Please ensure the item's Include is a port number, for example '<ContainerPort Include=\"80\" />'", port.ItemSpec);
                }
                else
                {
                    var message = "A ContainerPort item was provided with ";
                    var arguments = new List<string>(2);
                    if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber) && parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        message += "an invalid port number '{0}' and an invalid port type '{1}'";
                        arguments.Add(portNo);
                        arguments.Add(portType!);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        message += "an invalid port number '{0}'";
                        arguments.Add(portNo);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        message += "an invalid port type '{0}'";
                        arguments.Add(portType!);
                    }
                    message += ". ContainerPort items must have an Include value that is an integer, and a Type value that is either 'tcp' or 'udp'";

                    Log.LogError(message, arguments);
                }
            }
        }
    }

    private LocalDocker GetLocalDaemon(Action<string> logger) {
        var daemon = LocalContainerDaemon switch {
            KnownDaemonTypes.Docker => new LocalDocker(logger),
            _ => throw new NotSupportedException(
                Resource.FormatString(
                    nameof(Strings.UnknownDaemonType),
                    LocalContainerDaemon,
                    string.Join(",", KnownDaemonTypes.SupportedLocalDaemonTypes)))
        };
        return daemon;
    }

    private Lazy<Registry?> SourceRegistry
    {
        get {
            if(IsDaemonPull) {
                return new Lazy<Registry?>(() => null);
            } else {
                return new Lazy<Registry?>(() => new Registry(ContainerHelpers.TryExpandRegistryToUri(BaseRegistry)));
            }
        }
    }

    private Lazy<Registry?> DestinationRegistry {
        get {
            if(IsDaemonPush) {
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

    private ImageBuilder GetBaseImage()
    {
        if (SourceRegistry.Value is {} registry)
        {
            return registry.GetImageManifest(BaseImageName, BaseImageTag, ContainerRuntimeIdentifier, RuntimeIdentifierGraphPath).Result;
        }
        else
        {
            throw new ArgumentException("Don't know how to pull images from local daemons at the moment");
        }
    }

    private void SafeLog(string message, params object[] formatParams) {
        if(BuildEngine != null) Log.LogMessage(MessageImportance.High, message, formatParams);
    }
}

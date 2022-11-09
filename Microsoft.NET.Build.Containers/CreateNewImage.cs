using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.Tasks;

public partial class CreateNewImage : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolExe { get; set; }

    /// <summary>
    /// Unused. For interface parity with the ToolTask implementation of the task.
    /// </summary>
    public string ToolPath { get; set; }

    private bool IsDockerPush { get => String.IsNullOrEmpty(OutputRegistry); }

    private bool IsDockerPull { get => String.IsNullOrEmpty(BaseRegistry); }

    private void SetPorts(Image image, ITaskItem[] exposedPorts)
    {
        foreach (var port in exposedPorts)
        {
            var portNo = port.ItemSpec;
            var portTy = port.GetMetadata("Type");
            if (ContainerHelpers.TryParsePort(portNo, portTy, out var parsedPort, out var errors))
            {
                image.ExposePort(parsedPort.number, parsedPort.type);
            }
            else
            {
                ContainerHelpers.ParsePortError parsedErrors = (ContainerHelpers.ParsePortError)errors!;
                var portString = portTy == null ? portNo : $"{portNo}/{portTy}";
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
                        arguments.Add(portTy!);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        message += "an invalid port number '{0}'";
                        arguments.Add(portNo);
                    }
                    else if (parsedErrors.HasFlag(ContainerHelpers.ParsePortError.InvalidPortNumber))
                    {
                        message += "an invalid port type '{0}'";
                        arguments.Add(portTy!);
                    }
                    message += ". ContainerPort items must have an Include value that is an integer, and a Type value that is either 'tcp' or 'udp'";

                    Log.LogError(message, arguments);
                }
            }
        }
    }

    private void SetEnvironmentVariables(Image img, ITaskItem[] envVars)
    {
        foreach (ITaskItem envVar in envVars)
        {
            img.AddEnvironmentVariable(envVar.ItemSpec, envVar.GetMetadata("Value"));
        }
    }

    private Image GetBaseImage() {
        if (IsDockerPull) {
            throw new ArgumentException("Don't know how to pull images from local daemons at the moment");
        } else {
            var reg = new Registry(ContainerHelpers.TryExpandRegistryToUri(BaseRegistry));
            return reg.GetImageManifest(BaseImageName, BaseImageTag).Result;
        }
    }

    private void SafeLog(string message, params object[] formatParams) {
        if(BuildEngine != null) Log.LogMessage(MessageImportance.High, message, formatParams);
    }

    public override bool Execute()
    {
        if (!Directory.Exists(PublishDirectory))
        {
            Log.LogError("{0} '{1}' does not exist", nameof(PublishDirectory), PublishDirectory);
            return !Log.HasLoggedErrors;
        }

        var image = GetBaseImage();

        SafeLog("Building image '{0}' with tags {1} on top of base image {2}/{3}:{4}", ImageName, String.Join(",", ImageTags), BaseRegistry, BaseImageName, BaseImageTag);

        Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory);
        image.AddLayer(newLayer);
        image.WorkingDirectory = WorkingDirectory;
        image.SetEntrypoint(Entrypoint.Select(i => i.ItemSpec).ToArray(), EntrypointArgs.Select(i => i.ItemSpec).ToArray());

        foreach (var label in Labels)
        {
            image.Label(label.ItemSpec, label.GetMetadata("Value"));
        }

        SetEnvironmentVariables(image, ContainerEnvironmentVariables);

        SetPorts(image, ExposedPorts);

        // at the end of this step, if any failed then bail out.
        if (Log.HasLoggedErrors)
        {
            return false;
        }

        // at this point we're done with modifications and are just pushing the data other places
        GeneratedContainerManifest = image.manifest.ToJsonString();
        GeneratedContainerConfiguration = image.config.ToJsonString();

        Registry? outputReg = IsDockerPush ? null : new Registry(ContainerHelpers.TryExpandRegistryToUri(OutputRegistry));
        foreach (var tag in ImageTags)
        {
            if (IsDockerPush)
            {
                try
                {
                    LocalDocker.Load(image, ImageName, tag, BaseImageName).Wait();
                    SafeLog("Pushed container '{0}:{1}' to Docker daemon", ImageName, tag);
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
                    outputReg?.Push(image, ImageName, tag, BaseImageName, message => SafeLog(message)).Wait();
                    SafeLog("Pushed container '{0}:{1}' to registry '{2}'", ImageName, tag, OutputRegistry);
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
}
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.Tasks;

public class CreateNewImage : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// The base registry to pull from.
    /// Ex: https://mcr.microsoft.com
    /// </summary>
    [Required]
    public string BaseRegistry { get; set; }

    /// <summary>
    /// The base image to pull.
    /// Ex: dotnet/runtime
    /// </summary>
    [Required]
    public string BaseImageName { get; set; }

    /// <summary>
    /// The base image tag.
    /// Ex: 6.0
    /// </summary>
    [Required]
    public string BaseImageTag { get; set; }

    /// <summary>
    /// The registry to push to.
    /// </summary>
    [Required]
    public string OutputRegistry { get; set; }

    /// <summary>
    /// The name of the output image that will be pushed to the registry.
    /// </summary>
    [Required]
    public string ImageName { get; set; }

    /// <summary>
    /// The tag to associate with the new image.
    /// </summary>
    public string[] ImageTags { get; set; }

    /// <summary>
    /// The directory for the build outputs to be published.
    /// Constructed from "$(MSBuildProjectDirectory)\$(PublishDir)"
    /// </summary>
    [Required]
    public string PublishDirectory { get; set; }

    /// <summary>
    /// The working directory of the container.
    /// </summary>
    [Required]
    public string WorkingDirectory { get; set; }

    /// <summary>
    /// The entrypoint application of the container.
    /// </summary>
    [Required]
    public ITaskItem[] Entrypoint { get; set; }

    /// <summary>
    /// Arguments to pass alongside Entrypoint.
    /// </summary>
    public ITaskItem[] EntrypointArgs { get; set; }

    /// <summary>
    /// Labels that the image configuration will include in metadata
    /// </summary>
    public ITaskItem[] Labels { get; set; }

    public CreateNewImage()
    {
        BaseRegistry = "";
        BaseImageName = "";
        BaseImageTag = "";
        OutputRegistry = "";
        ImageName = "";
        ImageTags = Array.Empty<string>();
        PublishDirectory = "";
        WorkingDirectory = "";
        Entrypoint = Array.Empty<ITaskItem>();
        EntrypointArgs = Array.Empty<ITaskItem>();
        Labels = Array.Empty<ITaskItem>();
    }


    public override bool Execute()
    {
        if (!Directory.Exists(PublishDirectory))
        {
            Log.LogError("{0} '{1}' does not exist", nameof(PublishDirectory), PublishDirectory);
            return !Log.HasLoggedErrors;
        }

        Registry reg;
        Image image;

        try
        {
            reg = new Registry(new Uri(BaseRegistry, UriKind.RelativeOrAbsolute));
            image = reg.GetImageManifest(BaseImageName, BaseImageTag).Result;
        }
        catch
        {
            throw;
        }

        if (BuildEngine != null)
        {
            Log.LogMessage($"Loading from directory: {PublishDirectory}");
        }

        Layer newLayer = Layer.FromDirectory(PublishDirectory, WorkingDirectory);
        image.AddLayer(newLayer);
        image.WorkingDirectory = WorkingDirectory;
        image.SetEntrypoint(Entrypoint.Select(i => i.ItemSpec).ToArray(), EntrypointArgs.Select(i => i.ItemSpec).ToArray());

        foreach (var label in Labels)
        {
            image.Label(label.ItemSpec, label.GetMetadata("Value"));
        }

        var isDockerPush = OutputRegistry.StartsWith("docker://");
        Registry? outputReg = isDockerPush ? null : new Registry(new Uri(OutputRegistry));
        foreach (var tag in ImageTags)
        {
            if (isDockerPush)
            {
                try
                {
                    LocalDocker.Load(image, ImageName, tag, BaseImageName).Wait();
                    if (BuildEngine != null)
                    {
                        Log.LogMessage(MessageImportance.High, "Pushed container '{0}:{1}' to Docker daemon", ImageName, tag);
                    }
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
                    outputReg?.Push(image, ImageName, tag, BaseImageName).Wait();
                    if (BuildEngine != null)
                    {
                        Log.LogMessage(MessageImportance.High, "Pushed container '{0}:{1}' to registry '{2}'", ImageName, tag, OutputRegistry);
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
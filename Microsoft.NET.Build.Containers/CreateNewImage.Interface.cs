using System;

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.Tasks;

partial class CreateNewImage
{
    /// <summary>
    /// The path to the folder containing `containerize.dll`.
    /// </summary>
    /// <remarks>
    /// Used only for the ToolTask implementation of this task.
    /// </remarks>
    public string ContainerizeDirectory { get; set; }

    /// <summary>
    /// The base registry to pull from.
    /// Ex: mcr.microsoft.com
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
    /// Ports that the application declares that it will use.
    /// Note that this means nothing to container hosts, by default -
    /// it's mostly documentation.
    /// </summary>
    public ITaskItem[] ExposedPorts { get; set; }

    /// <summary>
    /// Labels that the image configuration will include in metadata
    /// </summary>
    public ITaskItem[] Labels { get; set; }

    /// <summary>
    /// Container environment variables to set.
    /// </summary>
    public ITaskItem[] ContainerEnvironmentVariables { get; set; }

    [Output]
    public string GeneratedContainerManifest { get; set; }

    [Output]
    public string GeneratedContainerConfiguration { get; set; }

    public CreateNewImage()
    {
        ContainerizeDirectory = "";
        ToolExe = "";
        ToolPath = "";
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
        ExposedPorts = Array.Empty<ITaskItem>();
        ContainerEnvironmentVariables = Array.Empty<ITaskItem>();
        GeneratedContainerConfiguration = "";
        GeneratedContainerManifest = "";
    }
}

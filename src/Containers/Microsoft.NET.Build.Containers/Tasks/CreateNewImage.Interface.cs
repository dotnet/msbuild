// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

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
    /// The file path to which to write a tar.gz archive of the container image.
    /// </summary>
    public string ArchiveOutputPath { get; set; }

    /// <summary>
    /// The kind of local registry to use, if any.
    /// </summary>
    public string LocalRegistry { get; set; }

    /// <summary>
    /// The name of the output image that will be pushed to the registry.
    /// </summary>
    [Required]
    public string Repository { get; set; }

    /// <summary>
    /// The tag to associate with the new image.
    /// </summary>
    [Required]
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
    /// Default arguments passed. These can be overridden by the user when the container is created.
    /// </summary>
    public ITaskItem[] DefaultArgs { get; set; }

    /// <summary>
    /// The file name and arguments that launch the application. For example: ['dotnet', 'app.dll'].
    /// </summary>
    public ITaskItem[] AppCommand { get; set; }

    /// <summary>
    /// Arguments always passed to the application.
    /// </summary>
    public ITaskItem[] AppCommandArgs { get; set; }

    /// <summary>
    /// The Dockerfile instruction used for AppCommand. Can be set to 'DefaultArgs', 'Entrypoint', 'None', '' (default).
    /// </summary>
    public string AppCommandInstruction { get; set; }

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

    /// <summary>
    /// The RID to use to determine the host manifest if the parent container is a manifest list
    /// </summary>
    [Required]
    public string ContainerRuntimeIdentifier { get; set; }

    /// <summary>
    /// The path to the runtime identifier graph file. This is used to compute RID compatibility for Image Manifest List entries.
    /// </summary>
    [Required]
    public string RuntimeIdentifierGraphPath { get; set; }

    /// <summary>
    /// The username or UID which is a platform-specific structure that allows specific control over which user the process run as.
    /// This acts as a default value to use when the value is not specified when creating a container.
    /// For Linux based systems, all of the following are valid: user, uid, user:group, uid:gid, uid:group, user:gid.
    /// If group/gid is not specified, the default group and supplementary groups of the given user/uid in /etc/passwd and /etc/group from the container are applied.
    /// If group/gid is specified, supplementary groups from the container are ignored.
    /// </summary>
    public string ContainerUser { get; set; }

    [Output]
    public string GeneratedContainerManifest { get; set; }

    [Output]
    public string GeneratedContainerConfiguration { get; set; }

    [Output]
    public string GeneratedContainerDigest { get; set; }

    [Output]
    public string GeneratedArchiveOutputPath { get; set; }

    public CreateNewImage()
    {
        ContainerizeDirectory = "";
        ToolExe = "";
        ToolPath = "";
        BaseRegistry = "";
        BaseImageName = "";
        BaseImageTag = "";
        OutputRegistry = "";
        ArchiveOutputPath = "";
        Repository = "";
        ImageTags = Array.Empty<string>();
        PublishDirectory = "";
        WorkingDirectory = "";
        Entrypoint = Array.Empty<ITaskItem>();
        EntrypointArgs = Array.Empty<ITaskItem>();
        DefaultArgs = Array.Empty<ITaskItem>();
        AppCommand = Array.Empty<ITaskItem>();
        AppCommandArgs = Array.Empty<ITaskItem>();
        AppCommandInstruction = "";
        Labels = Array.Empty<ITaskItem>();
        ExposedPorts = Array.Empty<ITaskItem>();
        ContainerEnvironmentVariables = Array.Empty<ITaskItem>();
        ContainerRuntimeIdentifier = "";
        RuntimeIdentifierGraphPath = "";
        LocalRegistry = "";
        ContainerUser = "";

        GeneratedContainerConfiguration = "";
        GeneratedContainerManifest = "";
        GeneratedContainerDigest = "";
        GeneratedArchiveOutputPath = "";

        TaskResources = Resource.Manager;
    }
}

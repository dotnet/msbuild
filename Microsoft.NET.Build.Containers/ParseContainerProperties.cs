using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.Tasks;

public class ParseContainerProperties : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// The full base image name. mcr.microsoft.com/dotnet/runtime:6.0, for example.
    /// </summary>
    [Required]
    public string FullyQualifiedBaseImageName { get; set; }

    /// <summary>
    /// The registry to push the new container to.
    /// </summary>
    [Required]
    public string ContainerRegistry { get; set; }

    /// <summary>
    /// The image name for the container to be created.
    /// </summary>
    [Required]
    public string ContainerImageName { get; set; }

    /// <summary>
    /// The tag for the container to be created.
    /// </summary>
    [Required]
    public string ContainerImageTag { get; set; }

    [Output]
    public string ParsedContainerRegistry { get; private set; }

    [Output]
    public string ParsedContainerImage { get; private set; }

    [Output]
    public string ParsedContainerTag { get; private set; }

    [Output]
    public string NewContainerRegistry { get; private set; }

    [Output]
    public string NewContainerImageName { get; private set; }

    [Output]
    public string NewContainerTag { get; private set; }

    public ParseContainerProperties()
    {
        FullyQualifiedBaseImageName = "";
        ContainerRegistry = "";
        ContainerImageName = "";
        ContainerImageTag = "";
        ParsedContainerRegistry = "";
        ParsedContainerImage = "";
        ParsedContainerTag = "";
        NewContainerRegistry = "";
        NewContainerImageName = "";
        NewContainerTag = "";
    }

    public override bool Execute()
    {
        if (!ContainerHelpers.IsValidImageName(ContainerImageName))
        {
            Log.LogError($"Invalid {nameof(ContainerImageName)}: {0}", ContainerImageName);
            return !Log.HasLoggedErrors;
        }

        if (!string.IsNullOrEmpty(ContainerImageTag) && !ContainerHelpers.IsValidImageTag(ContainerImageTag))
        {
            Log.LogError($"Invalid {nameof(ContainerImageTag)}: {0}", ContainerImageTag);
            return !Log.HasLoggedErrors;
        }

        if (FullyQualifiedBaseImageName.Contains(' ') && BuildEngine != null)
        {
            Log.LogWarning($"{nameof(FullyQualifiedBaseImageName)} had spaces in it, replacing with dashes.");
        }

        if (!ContainerHelpers.TryParseFullyQualifiedContainerName(FullyQualifiedBaseImageName.Replace(' ', '-'),
                                                                  out string? outputReg,
                                                                  out string? outputImage,
                                                                  out string? outputTag))
        {
            Log.LogError($"Could not parse {nameof(FullyQualifiedBaseImageName)}: {0}", FullyQualifiedBaseImageName);
            return !Log.HasLoggedErrors;
        }

        ParsedContainerRegistry = outputReg;
        ParsedContainerImage = outputImage;
        ParsedContainerTag = outputTag;
        NewContainerRegistry = ContainerRegistry;
        NewContainerImageName = ContainerImageName;
        NewContainerTag = ContainerImageTag;

        if (BuildEngine != null)
        {
            Log.LogMessage(MessageImportance.Low, "Parsed the following properties. Note: Spaces are replaced with dashes.");
            Log.LogMessage(MessageImportance.Low, "Host: {0}", ParsedContainerRegistry);
            Log.LogMessage(MessageImportance.Low, "Image: {0}", ParsedContainerImage);
            Log.LogMessage(MessageImportance.Low, "Tag: {0}", ParsedContainerTag);
            Log.LogMessage(MessageImportance.Low, "Image Name: {0}", NewContainerImageName);
            Log.LogMessage(MessageImportance.Low, "Image Tag: {0}", NewContainerTag);
        }

        return !Log.HasLoggedErrors;
    }
}

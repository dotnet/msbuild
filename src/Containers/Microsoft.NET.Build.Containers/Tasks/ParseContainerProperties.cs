// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed class ParseContainerProperties : Microsoft.Build.Utilities.Task
{
    /// <summary>
    /// The full base image name. mcr.microsoft.com/dotnet/runtime:6.0, for example.
    /// </summary>
    [Required]
    public string FullyQualifiedBaseImageName { get; set; }

    /// <summary>
    /// The registry to push the new container to. This will be null if the container is to be pushed to a local registry.
    /// </summary>
    public string ContainerRegistry { get; set; }

    /// <summary>
    /// The name of the container to be created.
    /// </summary>
    [Required]
    public string ContainerRepository { get; set; }

    /// <summary>
    /// The tag for the container to be created.
    /// </summary>
    public string ContainerImageTag { get; set; }
    /// <summary>
    /// The tags for the container to be created.
    /// </summary>
    public string[] ContainerImageTags { get; set; }

    /// <summary>
    /// Container environment variables to set.
    /// </summary>
    public ITaskItem[] ContainerEnvironmentVariables { get; set; }

    [Output]
    public string ParsedContainerRegistry { get; private set; }

    [Output]
    public string ParsedContainerImage { get; private set; }

    [Output]
    public string ParsedContainerTag { get; private set; }

    [Output]
    public string NewContainerRegistry { get; private set; }

    [Output]
    public string NewContainerRepository { get; private set; }

    [Output]
    public string[] NewContainerTags { get; private set; }

    [Output]
    public ITaskItem[] NewContainerEnvironmentVariables { get; private set; }

    public ParseContainerProperties()
    {
        FullyQualifiedBaseImageName = "";
        ContainerRegistry = "";
        ContainerRepository = "";
        ContainerImageTag = "";
        ContainerImageTags = Array.Empty<string>();
        ContainerEnvironmentVariables = Array.Empty<ITaskItem>();
        ParsedContainerRegistry = "";
        ParsedContainerImage = "";
        ParsedContainerTag = "";
        NewContainerRegistry = "";
        NewContainerRepository = "";
        NewContainerTags = Array.Empty<string>();
        NewContainerEnvironmentVariables = Array.Empty<ITaskItem>();

        TaskResources = Resource.Manager;
    }

    public override bool Execute()
    {
        string[] validTags;
        if (!String.IsNullOrEmpty(ContainerImageTag) && ContainerImageTags.Length >= 1)
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.AmbiguousTags), nameof(ContainerImageTag), nameof(ContainerImageTags));
            return !Log.HasLoggedErrors;
        }

        if (!String.IsNullOrEmpty(ContainerImageTag))
        {
            if (ContainerHelpers.IsValidImageTag(ContainerImageTag))
            {
                validTags = new[] { ContainerImageTag };
            }
            else
            {
                validTags = Array.Empty<string>();
                Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidTag), nameof(ContainerImageTag), ContainerImageTag);
            }
        }
        else if (ContainerImageTags.Length != 0 && TryValidateTags(ContainerImageTags, out var valids, out var invalids))
        {
            validTags = valids;
            if (invalids.Any())
            {
                Log.LogErrorWithCodeFromResources(nameof(Strings.InvalidTags), nameof(ContainerImageTags), String.Join(",", invalids));
                return !Log.HasLoggedErrors;
            }
        }
        else
        {
            validTags = Array.Empty<string>();
        }

        if (!String.IsNullOrEmpty(ContainerRegistry) && !ContainerHelpers.IsValidRegistry(ContainerRegistry))
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.CouldntRecognizeRegistry), ContainerRegistry);
            return !Log.HasLoggedErrors;
        }

        ValidateEnvironmentVariables();

        if (FullyQualifiedBaseImageName.Contains(' ') && BuildEngine != null)
        {
            Log.LogWarningWithCodeFromResources(nameof(Strings.BaseImageNameWithSpaces), nameof(FullyQualifiedBaseImageName));
        }
        FullyQualifiedBaseImageName = FullyQualifiedBaseImageName.Replace(' ', '-');

        if (!ContainerHelpers.TryParseFullyQualifiedContainerName(FullyQualifiedBaseImageName,
                                                                  out string? outputReg,
                                                                  out string? outputImage,
                                                                  out string? outputTag,
                                                                  out string? _outputDigest,
                                                                  out bool isRegistrySpecified))
        {
            Log.LogErrorWithCodeFromResources(nameof(Strings.BaseImageNameParsingFailed), nameof(FullyQualifiedBaseImageName), FullyQualifiedBaseImageName);
            return !Log.HasLoggedErrors;
        }

        if (!isRegistrySpecified)
        {
            Log.LogWarningWithCodeFromResources(nameof(Strings.BaseImageNameRegistryFallback), nameof(FullyQualifiedBaseImageName), ContainerHelpers.DockerRegistryAlias);
        }

        var (normalizedRepository, normalizationWarning, normalizationError) = ContainerHelpers.NormalizeRepository(ContainerRepository);
        if (normalizedRepository is not null)
        {
            NewContainerRepository = normalizedRepository;
        }
        if (normalizationWarning is (string warningMessageKey, object[] warningParams))
        {
            Log.LogMessageFromResources(warningMessageKey, warningParams);
        }

        if (normalizationError is (string errorMessageKey, object[] errorParams))
        {
            Log.LogErrorWithCodeFromResources(errorMessageKey, errorParams);
            return !Log.HasLoggedErrors;
        }

        ParsedContainerRegistry = outputReg ?? "";
        ParsedContainerImage = outputImage ?? "";
        ParsedContainerTag = outputTag ?? "";
        NewContainerRegistry = ContainerRegistry;
        NewContainerTags = validTags;

        if (BuildEngine != null)
        {
            Log.LogMessage(MessageImportance.Low, "Parsed the following properties. Note: Spaces are replaced with dashes.");
            Log.LogMessage(MessageImportance.Low, "Host: {0}", ParsedContainerRegistry);
            Log.LogMessage(MessageImportance.Low, "Image: {0}", ParsedContainerImage);
            Log.LogMessage(MessageImportance.Low, "Tag: {0}", ParsedContainerTag);
            Log.LogMessage(MessageImportance.Low, "Image Name: {0}", NewContainerRepository);
            Log.LogMessage(MessageImportance.Low, "Image Tags: {0}", String.Join(", ", NewContainerTags));
        }

        return !Log.HasLoggedErrors;
    }

    private void ValidateEnvironmentVariables()
    {
        var filteredEnvVars = ContainerEnvironmentVariables.Where((x) => ContainerHelpers.IsValidEnvironmentVariable(x.ItemSpec)).ToArray<ITaskItem>();
        var badEnvVars = ContainerEnvironmentVariables.Where((x) => !ContainerHelpers.IsValidEnvironmentVariable(x.ItemSpec));

        foreach (var badEnvVar in badEnvVars)
        {
            if (BuildEngine != null)
            {
                Log.LogWarningWithCodeFromResources(nameof(Strings.InvalidEnvVar), nameof(ContainerEnvironmentVariables), badEnvVar.ItemSpec);
            }
        }

        NewContainerEnvironmentVariables = new ITaskItem[filteredEnvVars.Length];

        for (int i = 0; i < filteredEnvVars.Length; i++)
        {
            NewContainerEnvironmentVariables[i] = filteredEnvVars[i];
        }
    }

    private static bool TryValidateTags(string[] inputTags, out string[] validTags, out string[] invalidTags)
    {
        var v = new List<string>();
        var i = new List<string>();
        foreach (var tag in inputTags)
        {
            if (ContainerHelpers.IsValidImageTag(tag))
            {
                v.Add(tag);
            }
            else
            {
                i.Add(tag);
            }
        }
        validTags = v.ToArray();
        invalidTags = i.ToArray();
        return invalidTags.Length == 0;
    }
}

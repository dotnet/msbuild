// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Tasks;

/// <summary>
/// This task will shell out to the net7.0-targeted application for VS scenarios.
/// </summary>
public partial class CreateNewImage : ToolTask, ICancelableTask
{
    // Unused, ToolExe is set via targets and overrides this.
    protected override string ToolName => "dotnet";

    private (bool success, string user, string pass) extractionInfo;

    private string DotNetPath
    {
        get
        {
            string path = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "";
            if (string.IsNullOrEmpty(path))
            {
                path = string.IsNullOrEmpty(ToolPath) ? "" : ToolPath;
            }

            return path;
        }
    }

    protected override string GenerateFullPathToTool() => Path.Combine(DotNetPath, ToolExe);

    /// <summary>
    /// Workaround to avoid storing user/pass into the EnvironmentVariables property, which gets logged by the task.
    /// </summary>
    /// <param name="pathToTool"></param>
    /// <param name="commandLineCommands"></param>
    /// <param name="responseFileSwitch"></param>
    /// <returns></returns>
    protected override ProcessStartInfo GetProcessStartInfo(string pathToTool, string commandLineCommands, string responseFileSwitch)
    {
        VSHostObject hostObj = new VSHostObject(HostObject as System.Collections.Generic.IEnumerable<ITaskItem>);
        if (hostObj.ExtractCredentials(out string user, out string pass, (string s) => Log.LogWarning(s)))
        {
            extractionInfo = (true, user, pass);
        }
        else
        {
            Log.LogMessage(MessageImportance.Low, Resource.GetString(nameof(Strings.HostObjectNotDetected)));
        }

        ProcessStartInfo startInfo = base.GetProcessStartInfo(pathToTool, commandLineCommands, responseFileSwitch)!;

        if (extractionInfo.success)
        {
            startInfo.Environment[ContainerHelpers.HostObjectUser] = extractionInfo.user;
            startInfo.Environment[ContainerHelpers.HostObjectPass] = extractionInfo.pass;
        }

        return startInfo;
    }

    protected override string GenerateCommandLineCommands() => GenerateCommandLineCommandsInt();

    /// <remarks>
    /// For unit test purposes
    /// </remarks>
    internal string GenerateCommandLineCommandsInt()
    {
        if (string.IsNullOrWhiteSpace(PublishDirectory))
        {
            throw new InvalidOperationException(Resource.FormatString(nameof(Strings.RequiredPropertyNotSetOrEmpty), nameof(PublishDirectory)));
        }
        if (string.IsNullOrWhiteSpace(BaseRegistry))
        {
            throw new InvalidOperationException(Resource.FormatString(nameof(Strings.RequiredPropertyNotSetOrEmpty), nameof(BaseRegistry)));
        }
        if (string.IsNullOrWhiteSpace(BaseImageName))
        {
            throw new InvalidOperationException(Resource.FormatString(nameof(Strings.RequiredPropertyNotSetOrEmpty), nameof(BaseImageName)));
        }
        if (string.IsNullOrWhiteSpace(Repository))
        {
            throw new InvalidOperationException(Resource.FormatString(nameof(Strings.RequiredPropertyNotSetOrEmpty), nameof(Repository)));
        }
        if (string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            throw new InvalidOperationException(Resource.FormatString(nameof(Strings.RequiredPropertyNotSetOrEmpty), nameof(WorkingDirectory)));
        }
        if (Entrypoint.Length == 0)
        {
            throw new InvalidOperationException(Resource.FormatString(nameof(Strings.RequiredItemsNotSet), nameof(Entrypoint)));
        }
        if (Entrypoint.Any(e => string.IsNullOrWhiteSpace(e.ItemSpec)))
        {
            throw new InvalidOperationException(Resource.FormatString(nameof(Strings.RequiredItemsContainsEmptyItems), nameof(Entrypoint)));
        }

        CommandLineBuilder builder = new();

        //mandatory options
        builder.AppendFileNameIfNotNull(Path.Combine(ContainerizeDirectory, "containerize.dll"));
        builder.AppendFileNameIfNotNull(PublishDirectory.TrimEnd(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }));
        builder.AppendSwitchIfNotNull("--baseregistry ", BaseRegistry);
        builder.AppendSwitchIfNotNull("--baseimagename ", BaseImageName);
        builder.AppendSwitchIfNotNull("--repository ", Repository);
        builder.AppendSwitchIfNotNull("--workingdirectory ", WorkingDirectory);
        ITaskItem[] sanitizedEntryPoints = Entrypoint.Where(e => !string.IsNullOrWhiteSpace(e.ItemSpec)).ToArray();
        builder.AppendSwitchIfNotNull("--entrypoint ", sanitizedEntryPoints, delimiter: " ");

        //optional options
        if (!string.IsNullOrWhiteSpace(BaseImageTag))
        {
            builder.AppendSwitchIfNotNull("--baseimagetag ", BaseImageTag);
        }
        if (!string.IsNullOrWhiteSpace(OutputRegistry))
        {
            builder.AppendSwitchIfNotNull("--outputregistry ", OutputRegistry);
        }
        if (!string.IsNullOrWhiteSpace(LocalContainerDaemon))
        {
            builder.AppendSwitchIfNotNull("--localcontainerdaemon ", LocalContainerDaemon);
        }

        if (EntrypointArgs.Any(e => string.IsNullOrWhiteSpace(e.ItemSpec)))
        {
            Log.LogWarningWithCodeFromResources(nameof(Strings.EmptyValuesIgnored), nameof(EntrypointArgs));
        }
        ITaskItem[] sanitizedEntryPointArgs = EntrypointArgs.Where(e => !string.IsNullOrWhiteSpace(e.ItemSpec)).ToArray();
        builder.AppendSwitchIfNotNull("--entrypointargs ", sanitizedEntryPointArgs, delimiter: " ");

        if (Labels.Any(e => string.IsNullOrWhiteSpace(e.ItemSpec)))
        {
            Log.LogWarningWithCodeFromResources(nameof(Strings.EmptyValuesIgnored), nameof(Labels));
        }
        var sanitizedLabels = Labels.Where(e => !string.IsNullOrWhiteSpace(e.ItemSpec));
        if (sanitizedLabels.Any(i => i.GetMetadata("Value") is null))
        {
            Log.LogWarningWithCodeFromResources(nameof(Strings.ItemsWithoutMetadata), nameof(Labels));
            sanitizedLabels = sanitizedLabels.Where(i => i.GetMetadata("Value") is not null);
        }

        string[] readyLabels = sanitizedLabels.Select(i => i.ItemSpec + "=" + i.GetMetadata("Value")).ToArray();
        builder.AppendSwitchIfNotNull("--labels ", readyLabels, delimiter: " ");

        if (ImageTags.Any(string.IsNullOrWhiteSpace))
        {
            Log.LogWarningWithCodeFromResources(nameof(Strings.EmptyOrWhitespacePropertyIgnored), nameof(ImageTags));
        }
        string[] sanitizedImageTags = ImageTags.Where(i => !string.IsNullOrWhiteSpace(i)).ToArray();
        builder.AppendSwitchIfNotNull("--imagetags ", sanitizedImageTags, delimiter: " ");

        if (ExposedPorts.Any(e => string.IsNullOrWhiteSpace(e.ItemSpec)))
        {
            Log.LogWarningWithCodeFromResources(nameof(Strings.EmptyValuesIgnored), nameof(ExposedPorts));
        }
        var sanitizedPorts = ExposedPorts.Where(e => !string.IsNullOrWhiteSpace(e.ItemSpec));
        string[] readyPorts =
            sanitizedPorts
                .Select(i => (i.ItemSpec, i.GetMetadata("Type")))
                .Select(pair => string.IsNullOrWhiteSpace(pair.Item2) ? pair.Item1 : (pair.Item1 + "/" + pair.Item2))
                .ToArray();
        builder.AppendSwitchIfNotNull("--ports ", readyPorts, delimiter: " ");

        if (ContainerEnvironmentVariables.Any(e => string.IsNullOrWhiteSpace(e.ItemSpec)))
        {
            Log.LogWarningWithCodeFromResources(nameof(Strings.EmptyValuesIgnored), nameof(ContainerEnvironmentVariables));
        }
        var sanitizedEnvVariables = ContainerEnvironmentVariables.Where(e => !string.IsNullOrWhiteSpace(e.ItemSpec));
        if (sanitizedEnvVariables.Any(i => i.GetMetadata("Value") is null))
        {
            Log.LogWarningWithCodeFromResources(nameof(Strings.ItemsWithoutMetadata), nameof(ContainerEnvironmentVariables));
            sanitizedEnvVariables = sanitizedEnvVariables.Where(i => i.GetMetadata("Value") is not null);
        }
        string[] readyEnvVariables = sanitizedEnvVariables.Select(i => i.ItemSpec + "=" + i.GetMetadata("Value")).ToArray();
        builder.AppendSwitchIfNotNull("--environmentvariables ", readyEnvVariables, delimiter: " ");

        if (!string.IsNullOrWhiteSpace(ContainerRuntimeIdentifier))
        {
            builder.AppendSwitchIfNotNull("--rid ", ContainerRuntimeIdentifier);
        }

        if (!string.IsNullOrWhiteSpace(RuntimeIdentifierGraphPath))
        {
            builder.AppendSwitchIfNotNull("--ridgraphpath ", RuntimeIdentifierGraphPath);
        }

        if (!string.IsNullOrWhiteSpace(ContainerUser))
        {
            builder.AppendSwitchIfNotNull("--container-user ", ContainerUser);
        }

        return builder.ToString();
    }
}


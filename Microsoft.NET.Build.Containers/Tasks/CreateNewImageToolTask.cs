// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

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

    protected override string GenerateFullPathToTool() => Quote(Path.Combine(DotNetPath, ToolExe));

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
            Log.LogMessage(MessageImportance.Low, "No host object detected.");
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
            throw new InvalidOperationException($"Required property '{nameof(PublishDirectory)}' was not set or empty.");
        }
        if (string.IsNullOrWhiteSpace(BaseRegistry))
        {
            throw new InvalidOperationException($"Required property '{nameof(BaseRegistry)}' was not set or empty.");
        }
        if (string.IsNullOrWhiteSpace(BaseImageName))
        {
            throw new InvalidOperationException($"Required property '{nameof(BaseImageName)}' was not set or empty.");
        }
        if (string.IsNullOrWhiteSpace(ImageName))
        {
            throw new InvalidOperationException($"Required property '{nameof(ImageName)}' was not set or empty.");
        }
        if (string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            throw new InvalidOperationException($"Required property '{nameof(WorkingDirectory)}' was not set or empty.");
        }
        if (Entrypoint.Length == 0)
        {
            throw new InvalidOperationException($"Required '{nameof(Entrypoint)}' items were not set.");
        }
        if (Entrypoint.Any(e => string.IsNullOrWhiteSpace(e.ItemSpec)))
        {
            throw new InvalidOperationException($"Required '{nameof(Entrypoint)}' items contain empty items.");
        }

        CommandLineBuilder builder = new();

        //mandatory options
        builder.AppendFileNameIfNotNull(Path.Combine(ContainerizeDirectory, "containerize.dll"));
        builder.AppendFileNameIfNotNull(PublishDirectory.TrimEnd(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }));
        builder.AppendSwitchIfNotNull("--baseregistry ", BaseRegistry);
        builder.AppendSwitchIfNotNull("--baseimagename ", BaseImageName);
        builder.AppendSwitchIfNotNull("--imagename ", ImageName);
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
            Log.LogWarning($"Items '{nameof(EntrypointArgs)}' contain empty item(s) which will be ignored.");
        }
        ITaskItem[] sanitizedEntryPointArgs = EntrypointArgs.Where(e => !string.IsNullOrWhiteSpace(e.ItemSpec)).ToArray();
        builder.AppendSwitchIfNotNull("--entrypointargs ", sanitizedEntryPointArgs, delimiter: " ");

        if (Labels.Any(e => string.IsNullOrWhiteSpace(e.ItemSpec)))
        {
            Log.LogWarning($"Items '{nameof(Labels)}' contain empty item(s) which will be ignored.");
        }
        var sanitizedLabels = Labels.Where(e => !string.IsNullOrWhiteSpace(e.ItemSpec));
        if (sanitizedLabels.Any(i => i.GetMetadata("Value") is null))
        {
            Log.LogWarning($"Item '{nameof(Labels)}' contains items without metadata 'Value', and they will be ignored.");
            sanitizedLabels = sanitizedLabels.Where(i => i.GetMetadata("Value") is not null);
        }

        string[] readyLabels = sanitizedLabels.Select(i => i.ItemSpec + "=" + Quote(i.GetMetadata("Value"))).ToArray();
        builder.AppendSwitchIfNotNull("--labels ", readyLabels, delimiter: " ");

        if (ImageTags.Any(string.IsNullOrWhiteSpace))
        {
            Log.LogWarning($"Property '{nameof(ImageTags)}' is empty or contains whitespace and will be ignored.");
        }
        string[] sanitizedImageTags = ImageTags.Where(i => !string.IsNullOrWhiteSpace(i)).ToArray();
        builder.AppendSwitchIfNotNull("--imagetags ", sanitizedImageTags, delimiter: " ");

        if (ExposedPorts.Any(e => string.IsNullOrWhiteSpace(e.ItemSpec)))
        {
            Log.LogWarning($"Items '{nameof(ExposedPorts)}' contain empty item(s) which will be ignored.");
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
            Log.LogWarning($"Items '{nameof(ContainerEnvironmentVariables)}' contain empty item(s) which will be ignored.");
        }
        var sanitizedEnvVariables = ContainerEnvironmentVariables.Where(e => !string.IsNullOrWhiteSpace(e.ItemSpec));
        if (sanitizedEnvVariables.Any(i => i.GetMetadata("Value") is null))
        {
            Log.LogWarning($"Item '{nameof(ContainerEnvironmentVariables)}' contains items without metadata 'Value', and they will be ignored.");
            sanitizedEnvVariables = sanitizedEnvVariables.Where(i => i.GetMetadata("Value") is not null);
        }
        string[] readyEnvVariables = sanitizedEnvVariables.Select(i => i.ItemSpec + "=" + Quote(i.GetMetadata("Value"))).ToArray();
        builder.AppendSwitchIfNotNull("--environmentvariables ", readyEnvVariables, delimiter: " ");

        if (!string.IsNullOrWhiteSpace(ContainerRuntimeIdentifier))
        {
            builder.AppendSwitchIfNotNull("--rid ", ContainerRuntimeIdentifier);
        }
        if (!string.IsNullOrWhiteSpace(RuntimeIdentifierGraphPath))
        {
            builder.AppendSwitchIfNotNull("--ridgraphpath ", RuntimeIdentifierGraphPath);
        }
        return builder.ToString();
    }

    private static string Quote(string path)
    {
        if (path.Length >= 2 && (path[0] == '\"' && path[path.Length - 1] == '\"'))
        {
            // it's already quoted
            return path;
        }
        return $"\"{path}\"";
    }
}

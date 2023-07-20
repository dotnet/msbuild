// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

using System.Collections.ObjectModel;
using Microsoft.NET.Build.Containers.Resources;

/// <summary>
/// The class builds new image based on the base image.
/// </summary>
internal sealed class ImageBuilder
{
    private readonly ManifestV2 _manifest;
    private readonly ImageConfig _baseImageConfig;

    public ImageConfig BaseImageConfig => _baseImageConfig;

    internal ImageBuilder(ManifestV2 manifest, ImageConfig baseImageConfig)
    {
        _manifest = manifest;
        _baseImageConfig = baseImageConfig;
    }

    /// <summary>
    /// Gets a value indicating whether the base image is has a Windows operating system.
    /// </summary>
    public bool IsWindows => _baseImageConfig.IsWindows;

    /// <summary>
    /// Builds the image configuration <see cref="BuiltImage"/> ready for further processing.
    /// </summary>
    internal BuiltImage Build()
    {
        string imageJsonStr = _baseImageConfig.BuildConfig();
        string imageSha = DigestUtils.GetSha(imageJsonStr);
        string imageDigest = DigestUtils.GetDigestFromSha(imageSha);
        long imageSize = Encoding.UTF8.GetBytes(imageJsonStr).Length;

        ManifestConfig newManifestConfig = _manifest.Config with
        {
            digest = imageDigest,
            size = imageSize
        };

        ManifestV2 newManifest = _manifest with
        {
            Config = newManifestConfig
        };

        return new BuiltImage()
        {
            Config = imageJsonStr,
            ImageDigest = imageDigest,
            ImageSha = imageSha,
            ImageSize = imageSize,
            Manifest = newManifest,
        };
    }

    /// <summary>
    /// Adds a <see cref="Layer"/> to a base image.
    /// </summary>
    internal void AddLayer(Layer l)
    {
        _manifest.Layers.Add(new(l.Descriptor.MediaType, l.Descriptor.Size, l.Descriptor.Digest, l.Descriptor.Urls));
        _baseImageConfig.AddLayer(l);
    }

    internal ReadOnlyDictionary<string, string> EnvironmentVariables => _baseImageConfig.EnvironmentVariables;

    /// <summary>
    /// Adds a label to a base image.
    /// </summary>
    internal void AddLabel(string name, string value) => _baseImageConfig.AddLabel(name, value);

    /// <summary>
    /// Adds environment variables to a base image.
    /// </summary>
    internal void AddEnvironmentVariable(string envVarName, string value) => _baseImageConfig.AddEnvironmentVariable(envVarName, value);

    /// <summary>
    /// Exposes additional port.
    /// </summary>
    internal void ExposePort(int number, PortType type) => _baseImageConfig.ExposePort(number, type);

    /// <summary>
    /// Sets working directory for the image.
    /// </summary>
    internal void SetWorkingDirectory(string workingDirectory) => _baseImageConfig.SetWorkingDirectory(workingDirectory);

    /// <summary>
    /// Sets the ENTRYPOINT and CMD for the image.
    /// </summary>
    internal void SetEntrypointAndCmd(string[] entrypoint, string[] cmd) => _baseImageConfig.SetEntrypointAndCmd(entrypoint, cmd);

    /// <summary>
    /// Sets the USER for the image.
    /// </summary>
    internal void SetUser(string user) => _baseImageConfig.SetUser(user);

    internal static (string[] entrypoint, string[] cmd) DetermineEntrypointAndCmd(
        string[] entrypoint,
        string[] entrypointArgs,
        string[] cmd,
        string[] appCommand,
        string[] appCommandArgs,
        string appCommandInstruction,
        string[]? baseImageEntrypoint,
        Action<string> logWarning,
        Action<string, string?> logError)
    {
        bool setsEntrypoint = entrypoint.Length > 0 || entrypointArgs.Length > 0;
        bool setsCmd = cmd.Length > 0;

        baseImageEntrypoint ??= Array.Empty<string>();
        // Some (Microsoft) base images set 'dotnet' as the ENTRYPOINT. We mustn't use it.
        if (baseImageEntrypoint.Length == 1 && (baseImageEntrypoint[0] == "dotnet" || baseImageEntrypoint[0] == "/usr/bin/dotnet"))
        {
            baseImageEntrypoint = Array.Empty<string>();
        }

        if (string.IsNullOrEmpty(appCommandInstruction))
        {
            if (setsEntrypoint)
            {
                // Backwards-compatibility: before 'AppCommand'/'Cmd' was added, only 'Entrypoint' was available.
                if (!setsCmd && appCommandArgs.Length == 0 && entrypoint.Length == 0)
                {
                    // Copy over the values for starting the application from AppCommand.
                    entrypoint = appCommand;
                    appCommand = Array.Empty<string>();

                    // Use EntrypointArgs as cmd.
                    cmd = entrypointArgs;
                    entrypointArgs = Array.Empty<string>();

                    if (entrypointArgs.Length > 0)
                    {
                        // Log warning: Instead of ContainerEntrypointArgs, use ContainerAppCommandArgs for arguments that must always be set, or ContainerDefaultArgs for default arguments that the user override when creating the container.
                        logWarning(nameof(Strings.EntrypointArgsSetPreferAppCommandArgs));
                    }

                    appCommandInstruction = KnownAppCommandInstructions.None;
                }
                else
                {
                    // There's an Entrypoint. Use DefaultArgs for the AppCommand.
                    appCommandInstruction = KnownAppCommandInstructions.DefaultArgs;
                }
            }
            else
            {
                // Default to use an Entrypoint.
                // If the base image defines an ENTRYPOINT, print a warning.
                if (baseImageEntrypoint.Length > 0)
                {
                    logWarning(nameof(Strings.BaseEntrypointOverwritten));
                }
                appCommandInstruction = KnownAppCommandInstructions.Entrypoint;
            }
        }

        if (entrypointArgs.Length > 0 && entrypoint.Length == 0)
        {
            logError(nameof(Strings.EntrypointArgsSetNoEntrypoint), null);
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        if (appCommandArgs.Length > 0 && appCommand.Length == 0)
        {
            logError(nameof(Strings.AppCommandArgsSetNoAppCommand), null);
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        switch (appCommandInstruction)
        {
            case KnownAppCommandInstructions.None:
                if (appCommand.Length > 0 || appCommandArgs.Length > 0)
                {
                    logError(nameof(Strings.AppCommandSetNotUsed), appCommandInstruction);
                    return (Array.Empty<string>(), Array.Empty<string>());
                }
                break;
            case KnownAppCommandInstructions.DefaultArgs:
                cmd = appCommand.Concat(appCommandArgs).Concat(cmd).ToArray();
                break;
            case KnownAppCommandInstructions.Entrypoint:
                if (setsEntrypoint)
                {
                    logError(nameof(Strings.EntrypointConflictAppCommand), appCommandInstruction);
                    return (Array.Empty<string>(), Array.Empty<string>());
                }
                entrypoint = appCommand;
                entrypointArgs = appCommandArgs;
                break;
            default:
                throw new NotSupportedException(
                    Resource.FormatString(
                        nameof(Strings.UnknownAppCommandInstruction),
                        appCommandInstruction,
                        string.Join(",", KnownAppCommandInstructions.SupportedAppCommandInstructions)));
        }

        return (entrypoint.Length > 0 ? entrypoint.Concat(entrypointArgs).ToArray() : baseImageEntrypoint, cmd);
    }
}

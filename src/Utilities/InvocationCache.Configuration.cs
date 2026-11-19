// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;

#pragma warning disable MSBUILD001

namespace Microsoft.Build.Utilities;

public sealed partial class InvocationCache
{
    /// <summary>
    /// Creates a cache only when the configured environment variable enables it.
    /// </summary>
    /// <param name="log">The task logger for cache diagnostics.</param>
    /// <param name="configuration">The tool name and environment variable names.</param>
    /// <returns>The cache, or <see langword="null"/> if disabled or the path cannot be resolved.</returns>
    public static InvocationCache? TryCreateFromEnvironment(TaskLoggingHelper log, InvocationCacheConfiguration configuration)
    {
        string? setting = Environment.GetEnvironmentVariable(configuration.EnabledEnvironmentVariable);
        if (string.IsNullOrEmpty(setting))
        {
            return null;
        }

        if (!bool.TryParse(setting, out bool enabled))
        {
            log.LogMessage(MessageImportance.Low, $"{configuration.ToolName} caching is disabled because {configuration.EnabledEnvironmentVariable} must be 'true' or 'false'.");
            return null;
        }

        return enabled ? TryCreate(Environment.GetEnvironmentVariable(configuration.PathEnvironmentVariable), log, configuration.ToolName) : null;
    }

    /// <summary>
    /// Resolves a cache path without creating storage.
    /// </summary>
    /// <param name="cacheDirectory">The override path, or null or empty to use the platform default.</param>
    /// <param name="log">The task logger for cache diagnostics.</param>
    /// <param name="toolName">The tool name.</param>
    /// <returns>The cache, or <see langword="null"/> if the path cannot be resolved.</returns>
    public static InvocationCache? TryCreate(string? cacheDirectory, TaskLoggingHelper log, string toolName)
    {
        if (string.IsNullOrEmpty(cacheDirectory))
        {
            cacheDirectory = InvocationCacheConfiguration.GetDefaultCacheDirectory(toolName);
        }

        if (cacheDirectory is null)
        {
            log.LogMessage(MessageImportance.Low, $"{toolName} caching is disabled because no default cache directory is available.");
            return null;
        }

        try
        {
            string fullPath = Path.GetFullPath(cacheDirectory);
            if (File.Exists(fullPath))
            {
                throw new IOException($"Cache location is a file: '{fullPath}'.");
            }

            return new InvocationCache(fullPath, log, toolName);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            log.LogMessage(MessageImportance.Low, $"{toolName} caching is disabled because the cache directory '{cacheDirectory}' could not be resolved: {ex.Message}");
            return null;
        }
    }
}

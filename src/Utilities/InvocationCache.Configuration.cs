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
        => TryCreateFromEnvironment(log, configuration, TaskEnvironment.Fallback);

    /// <summary>
    /// Creates a cache using the specified task's environment variables and project directory.
    /// </summary>
    /// <param name="log">The task logger for cache diagnostics.</param>
    /// <param name="configuration">The tool name and environment variable names.</param>
    /// <param name="environment">The environment used to read settings and resolve relative cache paths.</param>
    /// <returns>The cache, or <see langword="null"/> if disabled or the path cannot be resolved.</returns>
    public static InvocationCache? TryCreateFromEnvironment(TaskLoggingHelper log,
        InvocationCacheConfiguration configuration, TaskEnvironment environment)
    {
        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        string? setting = environment.GetEnvironmentVariable(configuration.EnabledEnvironmentVariable);
        if (string.IsNullOrEmpty(setting))
        {
            return null;
        }

        if (!bool.TryParse(setting, out bool enabled))
        {
            log.LogMessage(MessageImportance.Low, $"{configuration.ToolName} caching is disabled because {configuration.EnabledEnvironmentVariable} must be 'true' or 'false'.");
            return null;
        }

        return enabled
            ? TryCreate(environment.GetEnvironmentVariable(configuration.PathEnvironmentVariable), log, configuration.ToolName, environment)
            : null;
    }

    /// <summary>
    /// Resolves a cache path without creating storage.
    /// </summary>
    /// <param name="cacheDirectory">The override path, or null or empty to use the platform default.</param>
    /// <param name="log">The task logger for cache diagnostics.</param>
    /// <param name="toolName">The tool name.</param>
    /// <returns>The cache, or <see langword="null"/> if the path cannot be resolved.</returns>
    public static InvocationCache? TryCreate(string? cacheDirectory, TaskLoggingHelper log, string toolName)
        => TryCreate(cacheDirectory, log, toolName, TaskEnvironment.Fallback);

    /// <summary>
    /// Resolves a cache path using the specified task environment without creating storage.
    /// </summary>
    /// <param name="cacheDirectory">The override path, or null or empty to use the platform default.</param>
    /// <param name="log">The task logger for cache diagnostics.</param>
    /// <param name="toolName">The tool name.</param>
    /// <param name="environment">The environment used for default settings and relative path resolution.</param>
    /// <returns>The cache, or <see langword="null"/> if the path cannot be resolved.</returns>
    public static InvocationCache? TryCreate(string? cacheDirectory, TaskLoggingHelper log,
        string toolName, TaskEnvironment environment)
    {
        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        if (string.IsNullOrEmpty(cacheDirectory))
        {
            cacheDirectory = InvocationCacheConfiguration.GetDefaultCacheDirectory(toolName, environment);
        }

        if (cacheDirectory is null)
        {
            log.LogMessage(MessageImportance.Low, $"{toolName} caching is disabled because no default cache directory is available.");
            return null;
        }

        try
        {
            string fullPath = environment.GetAbsolutePath(cacheDirectory).Value;
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

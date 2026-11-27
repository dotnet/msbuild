// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Utilities;

/// <summary>
/// Names a tool and its cache enablement and path environment variables.
/// </summary>
[Experimental("MSBUILD001")]
public sealed class InvocationCacheConfiguration
{
    /// <summary>
    /// Gets the tool name used in cache messages, default paths, and writer locks.
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// Gets the environment variable that must contain <c>true</c> to enable caching.
    /// </summary>
    public string EnabledEnvironmentVariable { get; }

    /// <summary>
    /// Gets the environment variable overriding the platform's default cache path.
    /// </summary>
    public string PathEnvironmentVariable { get; }

    /// <summary>
    /// Creates a configuration without reading the environment.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="enabledEnvironmentVariable">The cache enablement variable.</param>
    /// <param name="pathEnvironmentVariable">The cache path override variable.</param>
    public InvocationCacheConfiguration(string toolName, string enabledEnvironmentVariable, string pathEnvironmentVariable)
    {
        ValidateToolName(toolName, nameof(toolName));
        if (enabledEnvironmentVariable is null)
        {
            throw new ArgumentNullException(nameof(enabledEnvironmentVariable));
        }

        if (string.IsNullOrWhiteSpace(enabledEnvironmentVariable))
        {
            throw new ArgumentException("The enabled environment variable name must not be empty or whitespace.", nameof(enabledEnvironmentVariable));
        }

        if (pathEnvironmentVariable is null)
        {
            throw new ArgumentNullException(nameof(pathEnvironmentVariable));
        }

        if (string.IsNullOrWhiteSpace(pathEnvironmentVariable))
        {
            throw new ArgumentException("The path environment variable name must not be empty or whitespace.", nameof(pathEnvironmentVariable));
        }

        ToolName = toolName;
        EnabledEnvironmentVariable = enabledEnvironmentVariable;
        PathEnvironmentVariable = pathEnvironmentVariable;
    }

    internal static void ValidateToolName(string toolName, string paramName)
    {
        if (toolName is null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (string.IsNullOrWhiteSpace(toolName) ||
            toolName is "." or ".." ||
            toolName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            toolName.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new ArgumentException($"The tool name '{toolName}' must be a valid single path segment.", paramName);
        }
    }

    internal static string? GetDefaultCacheDirectory(string toolName, TaskEnvironment environment)
    {
        ValidateToolName(toolName, nameof(toolName));
        string cacheDirectoryName = toolName.ToLowerInvariant();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string? localAppData = environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (string.IsNullOrEmpty(localAppData) || !Path.IsPathRooted(localAppData))
            {
                localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify);
            }

            return string.IsNullOrEmpty(localAppData) || !Path.IsPathRooted(localAppData)
                ? null
                : Path.Combine(localAppData, cacheDirectoryName);
        }

        bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        if (!isMacOS)
        {
            string? cacheHome = environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (!string.IsNullOrEmpty(cacheHome) && Path.IsPathRooted(cacheHome))
            {
                return Path.Combine(cacheHome, cacheDirectoryName);
            }
        }

        string? home = environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home) || !Path.IsPathRooted(home))
        {
            home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
        }

        if (string.IsNullOrEmpty(home) || !Path.IsPathRooted(home))
        {
            return null;
        }

        return isMacOS
            ? Path.Combine(home, "Library", "Caches", cacheDirectoryName)
            : Path.Combine(home, ".cache", cacheDirectoryName);
    }
}

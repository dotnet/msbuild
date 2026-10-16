// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

global using NativeMethodsShared = Microsoft.Build.Framework.NativeMethods;

using System;
using System.IO;

namespace Microsoft.Build.UnitTests.Shared;

[System.AttributeUsage(System.AttributeTargets.Assembly)]
internal sealed class BootstrapLocationAttribute(string bootstrapRoot, string bootstrapMsBuildBinaryLocation, string bootstrapSdkVersion, string latestDotNetCoreForMSBuild) : System.Attribute
{
    /// <summary>
    /// Path to the root of the bootstrap MSBuild (in artifacts folder).
    /// </summary>
    public string BootstrapRoot { get; } = bootstrapRoot;

    /// <summary>
    /// Resolves path to MSBuild[.exe], depending on the runtime and OS.
    /// </summary>
    public string BootstrapMsBuildBinaryLocation { get; } = bootstrapMsBuildBinaryLocation;

    /// <summary>
    /// Returns the version of the SDK used by the bootstrap MSBuild.
    /// </summary>
    public string BootstrapSdkVersion { get; } = ReadBootstrapSdkVersion(bootstrapRoot, bootstrapSdkVersion);

    internal string PinnedBootstrapSdkVersion { get; } = bootstrapSdkVersion;

    internal bool UsesNonPinnedSdk => !string.Equals(BootstrapSdkVersion, PinnedBootstrapSdkVersion, StringComparison.Ordinal);

    /// <summary>
    /// The latest .NET target framework setup in MSBuild.
    /// </summary>
    public string LatestDotNetCoreForMSBuild { get; } = latestDotNetCoreForMSBuild;

    private static string ReadBootstrapSdkVersion(string bootstrapRoot, string pinnedVersion)
    {
        string coreDirectory = Path.Combine(bootstrapRoot, "core");
        string versionFile = Path.Combine(coreDirectory, "bootstrap-sdk.version");
        if (!File.Exists(versionFile))
        {
            return pinnedVersion;
        }

        string version = File.ReadAllText(versionFile).Trim();
        if (version.Length == 0 ||
            version is "." or ".." ||
            version.IndexOfAny(['/', '\\']) >= 0 ||
            !Directory.Exists(Path.Combine(coreDirectory, "sdk", version)))
        {
            throw new InvalidOperationException($"Bootstrap SDK metadata '{versionFile}' does not identify an installed SDK. Rebuild the bootstrap.");
        }

        return version;
    }
}

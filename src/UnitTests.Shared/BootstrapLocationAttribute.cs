// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

global using NativeMethodsShared = Microsoft.Build.Framework.NativeMethods;

namespace Microsoft.Build.UnitTests.Shared;

[System.AttributeUsage(System.AttributeTargets.Assembly)]
internal sealed class BootstrapLocationAttribute(string bootstrapRoot, string bootstrapMsBuildBinaryLocation, string bootstrapSdkVersion) : System.Attribute
{
    /// <summary>
    /// Path to the root of the bootstrap MSBuild (in artifacts folder).
    /// </summary>
    public string BootstrapRoot { get; } = bootstrapRoot;

    /// <summary>
    /// Resolves path to MSBuild.exe or MSBuild.dll, depending on the runtime.
    /// </summary>
    public string BootstrapMsBuildBinaryLocation { get; } = bootstrapMsBuildBinaryLocation;

    /// <summary>
    /// Returns the version of the SDK used by the bootstrap MSBuild.
    /// </summary>
    public string BootstrapSdkVersion { get; } = bootstrapSdkVersion;
}

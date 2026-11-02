// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Utilities;

/// <summary>
/// ToolLocationHelper provides utility methods for locating .NET Framework and .NET Framework SDK directories and files.
/// NOTE: All public methods of this class are available to MSBuild projects for use in functions - they must be safe for
/// use during project evaluation.
/// </summary>
public static partial class ToolLocationHelper
{
    /// <summary>
    /// Lock object to synchronize chainedReferenceAssemblyPath dictionary.
    /// </summary>
    private static readonly LockType s_locker = new LockType();

    /// <summary>
    /// The current ToolsVersion.
    /// </summary>
    public static string CurrentToolsVersion => MSBuildConstants.CurrentToolsVersion;

    /// <summary>
    /// Clear our the appdomain wide caches
    /// </summary>
    internal static void ClearStaticCaches()
    {
        lock (s_locker)
        {
            s_chainedReferenceAssemblyPath?.Clear();
            s_cachedHighestFrameworkNameForTargetFrameworkIdentifier?.Clear();
            s_targetFrameworkMonikers?.Clear();
            s_cachedTargetFrameworkDisplayNames?.Clear();
            s_cachedReferenceAssemblyPaths?.Clear();
            s_cachedTargetPlatforms?.Clear();
            s_cachedTargetPlatformReferences?.Clear();
            s_cachedExtensionSdks?.Clear();
            s_cachedExtensionSdkReferences?.Clear();
        }
    }
}

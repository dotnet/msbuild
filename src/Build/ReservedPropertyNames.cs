// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Internal;

/// <summary>
///  Contains names for reserved and other well-known properties, together with related configuration
///  constants used by MSBuild.
/// </summary>
internal static class ReservedPropertyNames
{
    private const string MSBuild = nameof(MSBuild);
    private const string ProjectPrefix = $"{MSBuild}Project";
    private const string ThisFilePrefix = $"{MSBuild}ThisFile";

    // Keep reserved property constants synchronized with s_reservedProperties below.
    internal const string ProjectDirectory = $"{ProjectPrefix}Directory";
    internal const string ProjectDirectoryNoRoot = $"{ProjectPrefix}DirectoryNoRoot";
    internal const string ProjectFile = $"{ProjectPrefix}File";
    internal const string ProjectExtension = $"{ProjectPrefix}Extension";
    internal const string ProjectFullPath = $"{ProjectPrefix}FullPath";
    internal const string ProjectName = $"{ProjectPrefix}Name";

    internal const string ThisFileDirectory = $"{ThisFilePrefix}Directory";
    internal const string ThisFileDirectoryNoRoot = $"{ThisFilePrefix}DirectoryNoRoot";
    internal const string ThisFile = ThisFilePrefix; // We intentionally use MSBuildThisFile rather than MSBuildThisFileFile.
    internal const string ThisFileExtension = $"{ThisFilePrefix}Extension";
    internal const string ThisFileFullPath = $"{ThisFilePrefix}FullPath";
    internal const string ThisFileName = $"{ThisFilePrefix}Name";

    internal const string BinPath = "MSBuildBinPath";
    internal const string ProjectDefaultTargets = "MSBuildProjectDefaultTargets";
    internal const string ExtensionsPath = "MSBuildExtensionsPath";
    internal const string ExtensionsPath32 = "MSBuildExtensionsPath32";
    internal const string ExtensionsPath64 = "MSBuildExtensionsPath64";
    internal const string UserExtensionsPath = "MSBuildUserExtensionsPath";
    internal const string ToolsPath = MSBuildConstants.ToolsPath;
    internal const string ToolsVersion = "MSBuildToolsVersion";
    internal const string MSBuildRuntimeType = "MSBuildRuntimeType";
    internal const string OverrideTasksPath = "MSBuildOverrideTasksPath";
    internal const string DefaultOverrideToolsVersion = "DefaultOverrideToolsVersion";
    internal const string StartupDirectory = "MSBuildStartupDirectory";
    internal const string BuildNodeCount = "MSBuildNodeCount";
    internal const string LastTaskResult = "MSBuildLastTaskResult";
    internal const string ExtensionsPathSuffix = "MSBuild";
    internal const string UserExtensionsPathSuffix = @"Microsoft\MSBuild";
    internal const string ProgramFiles32 = "MSBuildProgramFiles32";
    internal const string LocalAppData = "LocalAppData";
    internal const string AssemblyVersion = "MSBuildAssemblyVersion";
    internal const string FileVersion = "MSBuildFileVersion";
    internal const string SemanticVersion = "MSBuildSemanticVersion";
    internal const string Version = "MSBuildVersion";
    internal const string OSName = "OS";
    internal const string FrameworkToolsRoot = "MSBuildFrameworkToolsRoot";
    internal const string Interactive = "MSBuildInteractive";
    internal const string MSBuildDisableFeaturesFromVersion = "MSBuildDisableFeaturesFromVersion";

    /// <summary>
    ///  Contains property names that only MSBuild may set.
    /// </summary>
    /// <remarks>
    ///  The <c>MSBuildExtensionsPath*</c> and <c>MSBuildUserExtensionsPath</c> properties are
    ///  intentionally excluded because tasks must be able to override them.
    /// </remarks>
    private static readonly HashSet<string> s_reservedProperties =
    [
        with(MSBuildNameIgnoreCaseComparer.Default),
        ProjectDirectory,
        ProjectDirectoryNoRoot,
        ProjectFile,
        ProjectExtension,
        ProjectFullPath,
        ProjectName,

        ThisFileDirectory,
        ThisFileDirectoryNoRoot,
        ThisFile,
        ThisFileExtension,
        ThisFileFullPath,
        ThisFileName,

        BinPath,
        ProjectDefaultTargets,
        ToolsPath,
        ToolsVersion,
        MSBuildRuntimeType,
        StartupDirectory,
        BuildNodeCount,
        LastTaskResult,
        ProgramFiles32,
        AssemblyVersion,
        Version,
        Interactive,
        MSBuildDisableFeaturesFromVersion,
    ];

    /// <summary>
    ///  Determines whether a property may be set only by MSBuild.
    /// </summary>
    /// <param name="property">The property name to examine.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="property"/> is reserved; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    internal static bool IsReservedProperty(string property)
        => s_reservedProperties.Contains(property);
}

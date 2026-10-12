// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Internal;

/// <summary>
///  Contains names for reserved and other well-known properties, together with related configuration
///  constants used by MSBuild.
/// </summary>
internal static class ReservedPropertyNames
{
    private const string MSBuild = "MSBuild";
    private const string ProjectPrefix = "MSBuildProject";
    private const string ThisFilePrefix = "MSBuildThisFile";

    // Keep these reserved property constants synchronized with ReservedPropertyKind and TryGetKind.
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

    internal const string BinPath = $"{MSBuild}BinPath";
    internal const string ProjectDefaultTargets = $"{MSBuild}ProjectDefaultTargets";
    internal const string ToolsPath = MSBuildConstants.ToolsPath;
    internal const string ToolsVersion = $"{MSBuild}ToolsVersion";
    internal const string MSBuildRuntimeType = $"{MSBuild}RuntimeType";
    internal const string StartupDirectory = $"{MSBuild}StartupDirectory";
    internal const string BuildNodeCount = $"{MSBuild}NodeCount";
    internal const string LastTaskResult = $"{MSBuild}LastTaskResult";
    internal const string ProgramFiles32 = $"{MSBuild}ProgramFiles32";
    internal const string AssemblyVersion = $"{MSBuild}AssemblyVersion";
    internal const string Version = $"{MSBuild}Version";
    internal const string Interactive = $"{MSBuild}Interactive";
    internal const string MSBuildDisableFeaturesFromVersion = $"{MSBuild}DisableFeaturesFromVersion";

    // These property names are intentionally not reserved.
    internal const string ExtensionsPath = $"{MSBuild}ExtensionsPath";
    internal const string ExtensionsPath32 = $"{MSBuild}ExtensionsPath32";
    internal const string ExtensionsPath64 = $"{MSBuild}ExtensionsPath64";
    internal const string UserExtensionsPath = $"{MSBuild}UserExtensionsPath";
    internal const string LocalAppData = "LocalAppData";
    internal const string FileVersion = $"{MSBuild}FileVersion";
    internal const string SemanticVersion = $"{MSBuild}SemanticVersion";
    internal const string OSName = "OS";
    internal const string FrameworkToolsRoot = $"{MSBuild}FrameworkToolsRoot";

    // Related configuration names and path suffixes.
    internal const string OverrideTasksPath = $"{MSBuild}OverrideTasksPath";
    internal const string DefaultOverrideToolsVersion = "DefaultOverrideToolsVersion";
    internal const string ExtensionsPathSuffix = MSBuild;
    internal const string UserExtensionsPathSuffix = $@"Microsoft\{MSBuild}";

    /// <summary>
    ///  Determines whether a property may be set only by MSBuild.
    /// </summary>
    /// <param name="property">The property name to examine.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="property"/> is reserved; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    internal static bool IsReservedProperty(string property)
        => TryGetKind(property, out _);

    /// <summary>
    ///  Attempts to identify a reserved MSBuild property.
    /// </summary>
    /// <param name="propertyName">The property name to examine.</param>
    /// <param name="result">The identified property, or <see cref="ReservedPropertyKind.None"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="propertyName"/> is reserved; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  The <c>MSBuildExtensionsPath*</c> and <c>MSBuildUserExtensionsPath</c> properties are
    ///  intentionally excluded because tasks must be able to override them.
    /// </remarks>
    public static bool TryGetKind(string propertyName, out ReservedPropertyKind result)
    {
        const int PrefixLength = 7; // MSBuild.Length

        if (propertyName.Length < 14 || !MatchesAt(propertyName, MSBuild, 0))
        {
            result = ReservedPropertyKind.None;
            return false;
        }

        char nextChar = propertyName[PrefixLength];

        if (nextChar is 'P' or 'p' && MatchesAt(propertyName, "Project", PrefixLength))
        {
            result = GetProjectPropertyKind(propertyName);
            return result is not ReservedPropertyKind.None;
        }

        if (nextChar is 'T' or 't' && MatchesAt(propertyName, "ThisFile", PrefixLength))
        {
            result = GetThisFilePropertyKind(propertyName);
            return result is not ReservedPropertyKind.None;
        }

        result = propertyName.Length switch
        {
            14 => nextChar switch
            {
                // MSBuildBinPath
                'B' or 'b' when MatchesAt(propertyName, "BinPath", PrefixLength)
                    => ReservedPropertyKind.BinPath,

                // MSBuildVersion
                'V' or 'v' when MatchesAt(propertyName, "Version", PrefixLength)
                    => ReservedPropertyKind.Version,

                _ => ReservedPropertyKind.None,
            },

            16 => nextChar switch
            {
                // MSBuildNodeCount
                'N' or 'n' when MatchesAt(propertyName, "NodeCount", PrefixLength)
                    => ReservedPropertyKind.BuildNodeCount,

                // MSBuildToolsPath
                'T' or 't' when MatchesAt(propertyName, "ToolsPath", PrefixLength)
                    => ReservedPropertyKind.ToolsPath,

                _ => ReservedPropertyKind.None,
            },

            18 => nextChar switch
            {
                // MSBuildInteractive
                'I' or 'i' when MatchesAt(propertyName, "Interactive", PrefixLength)
                    => ReservedPropertyKind.Interactive,

                // MSBuildRuntimeType
                'R' or 'r' when MatchesAt(propertyName, "RuntimeType", PrefixLength)
                    => ReservedPropertyKind.RuntimeType,

                _ => ReservedPropertyKind.None,
            },

            // MSBuildToolsVersion
            19 when MatchesAt(propertyName, "ToolsVersion", PrefixLength)
                => ReservedPropertyKind.ToolsVersion,

            21 => nextChar switch
            {
                // MSBuildLastTaskResult
                'L' or 'l' when MatchesAt(propertyName, "LastTaskResult", PrefixLength)
                    => ReservedPropertyKind.LastTaskResult,

                // MSBuildProgramFiles32
                'P' or 'p' when MatchesAt(propertyName, "ProgramFiles32", PrefixLength)
                    => ReservedPropertyKind.ProgramFiles32,

                _ => ReservedPropertyKind.None,
            },

            // MSBuildAssemblyVersion
            22 when MatchesAt(propertyName, "AssemblyVersion", PrefixLength)
                => ReservedPropertyKind.AssemblyVersion,

            // MSBuildStartupDirectory
            23 when MatchesAt(propertyName, "StartupDirectory", PrefixLength)
                => ReservedPropertyKind.StartupDirectory,

            // MSBuildDisableFeaturesFromVersion
            33 when MatchesAt(propertyName, "DisableFeaturesFromVersion", PrefixLength)
                => ReservedPropertyKind.DisableFeaturesFromVersion,

            _ => ReservedPropertyKind.None,
        };

        return result is not ReservedPropertyKind.None;

        static ReservedPropertyKind GetProjectPropertyKind(string propertyName)
        {
            const int ProjectPrefixLength = 14; // ProjectPrefix.Length

            return propertyName.Length switch
            {
                18 => propertyName[ProjectPrefixLength] switch
                {
                    // MSBuildProjectFile
                    'F' or 'f' when MatchesAt(propertyName, "File", ProjectPrefixLength)
                        => ReservedPropertyKind.ProjectFile,

                    // MSBuildProjectName
                    'N' or 'n' when MatchesAt(propertyName, "Name", ProjectPrefixLength)
                        => ReservedPropertyKind.ProjectName,

                    _ => ReservedPropertyKind.None,
                },

                // MSBuildProjectFullPath
                22 when MatchesAt(propertyName, "FullPath", ProjectPrefixLength)
                    => ReservedPropertyKind.ProjectFullPath,

                23 => propertyName[ProjectPrefixLength] switch
                {
                    // MSBuildProjectDirectory
                    'D' or 'd' when MatchesAt(propertyName, "Directory", ProjectPrefixLength)
                        => ReservedPropertyKind.ProjectDirectory,

                    // MSBuildProjectExtension
                    'E' or 'e' when MatchesAt(propertyName, "Extension", ProjectPrefixLength)
                        => ReservedPropertyKind.ProjectExtension,

                    _ => ReservedPropertyKind.None,
                },

                // MSBuildProjectDefaultTargets
                28 when MatchesAt(propertyName, "DefaultTargets", ProjectPrefixLength)
                    => ReservedPropertyKind.ProjectDefaultTargets,

                // MSBuildProjectDirectoryNoRoot
                29 when MatchesAt(propertyName, "DirectoryNoRoot", ProjectPrefixLength)
                    => ReservedPropertyKind.ProjectDirectoryNoRoot,

                _ => ReservedPropertyKind.None,
            };
        }

        static ReservedPropertyKind GetThisFilePropertyKind(string propertyName)
        {
            const int ThisFilePrefixLength = 15; // ThisFilePrefix.Length

            return propertyName.Length switch
            {
                // MSBuildThisFile
                15 => ReservedPropertyKind.ThisFile,

                // MSBuildThisFileName
                19 when MatchesAt(propertyName, "Name", ThisFilePrefixLength)
                    => ReservedPropertyKind.ThisFileName,

                // MSBuildThisFileFullPath
                23 when MatchesAt(propertyName, "FullPath", ThisFilePrefixLength)
                    => ReservedPropertyKind.ThisFileFullPath,

                24 => propertyName[ThisFilePrefixLength] switch
                {
                    // MSBuildThisFileDirectory
                    'D' or 'd' when MatchesAt(propertyName, "Directory", ThisFilePrefixLength)
                        => ReservedPropertyKind.ThisFileDirectory,

                    // MSBuildThisFileExtension
                    'E' or 'e' when MatchesAt(propertyName, "Extension", ThisFilePrefixLength)
                        => ReservedPropertyKind.ThisFileExtension,

                    _ => ReservedPropertyKind.None,
                },

                // MSBuildThisFileDirectoryNoRoot
                30 when MatchesAt(propertyName, "DirectoryNoRoot", ThisFilePrefixLength)
                    => ReservedPropertyKind.ThisFileDirectoryNoRoot,

                _ => ReservedPropertyKind.None,
            };
        }

        static bool MatchesAt(string propertyName, string expected, int startIndex)
            => propertyName.Length >= startIndex + expected.Length
                && MSBuildNameIgnoreCaseComparer.Default.Equals(expected, propertyName, startIndex, expected.Length);
    }
}

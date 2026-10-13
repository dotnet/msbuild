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
    ///  Determines whether a property name starts with <c>MSBuild</c>, ignoring case.
    /// </summary>
    /// <param name="propertyName">The property name to examine.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="propertyName"/> starts with <c>MSBuild</c>
    ///  and contains at least one additional character; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool StartsWithMSBuild(string propertyName)
        => StartsWithMSBuild(propertyName, startIndex: 0, propertyName.Length);

    /// <summary>
    ///  Determines whether a segment of a string starts with <c>MSBuild</c>, ignoring case.
    /// </summary>
    /// <param name="propertyName">The string containing the property name.</param>
    /// <param name="startIndex">The zero-based start index of the property name.</param>
    /// <param name="length">The length of the property name.</param>
    /// <returns>
    ///  <see langword="true"/> if the specified segment starts with <c>MSBuild</c> and contains
    ///  at least one additional character; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool StartsWithMSBuild(string propertyName, int startIndex, int length)
        => length > MSBuild.Length && MSBuildNameIgnoreCaseComparer.Default.Equals(MSBuild, propertyName, startIndex, MSBuild.Length);

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
        => TryGetKind(propertyName, 0, propertyName.Length, out result);

    /// <summary>
    ///  Attempts to identify a reserved MSBuild property in a segment of a string.
    /// </summary>
    /// <param name="propertyName">The string containing the property name.</param>
    /// <param name="startIndex">The zero-based start index of the property name.</param>
    /// <param name="length">The length of the property name.</param>
    /// <param name="result">The identified property, or <see cref="ReservedPropertyKind.None"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if the specified segment identifies a reserved property; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public static bool TryGetKind(string propertyName, int startIndex, int length, out ReservedPropertyKind result)
    {
        const int PrefixLength = 7; // MSBuild.Length

        int endIndex = startIndex + length;

        if (length < 14 || !MatchesAt(propertyName, MSBuild, startIndex, endIndex))
        {
            result = ReservedPropertyKind.None;
            return false;
        }

        char nextChar = propertyName[startIndex + PrefixLength];

        if (nextChar is 'P' or 'p' && MatchesAt(propertyName, "Project", startIndex + PrefixLength, endIndex))
        {
            result = GetProjectPropertyKind(propertyName, startIndex, length);
            return result is not ReservedPropertyKind.None;
        }

        if (nextChar is 'T' or 't' && MatchesAt(propertyName, "ThisFile", startIndex + PrefixLength, endIndex))
        {
            result = GetThisFilePropertyKind(propertyName, startIndex, length);
            return result is not ReservedPropertyKind.None;
        }

        result = length switch
        {
            14 => nextChar switch
            {
                // MSBuildBinPath
                'B' or 'b' when MatchesAt(propertyName, "BinPath", startIndex + PrefixLength, endIndex)
                    => ReservedPropertyKind.BinPath,

                // MSBuildVersion
                'V' or 'v' when MatchesAt(propertyName, "Version", startIndex + PrefixLength, endIndex)
                    => ReservedPropertyKind.Version,

                _ => ReservedPropertyKind.None,
            },

            16 => nextChar switch
            {
                // MSBuildNodeCount
                'N' or 'n' when MatchesAt(propertyName, "NodeCount", startIndex + PrefixLength, endIndex)
                    => ReservedPropertyKind.BuildNodeCount,

                // MSBuildToolsPath
                'T' or 't' when MatchesAt(propertyName, "ToolsPath", startIndex + PrefixLength, endIndex)
                    => ReservedPropertyKind.ToolsPath,

                _ => ReservedPropertyKind.None,
            },

            18 => nextChar switch
            {
                // MSBuildInteractive
                'I' or 'i' when MatchesAt(propertyName, "Interactive", startIndex + PrefixLength, endIndex)
                    => ReservedPropertyKind.Interactive,

                // MSBuildRuntimeType
                'R' or 'r' when MatchesAt(propertyName, "RuntimeType", startIndex + PrefixLength, endIndex)
                    => ReservedPropertyKind.RuntimeType,

                _ => ReservedPropertyKind.None,
            },

            // MSBuildToolsVersion
            19 when MatchesAt(propertyName, "ToolsVersion", startIndex + PrefixLength, endIndex)
                => ReservedPropertyKind.ToolsVersion,

            21 => nextChar switch
            {
                // MSBuildLastTaskResult
                'L' or 'l' when MatchesAt(propertyName, "LastTaskResult", startIndex + PrefixLength, endIndex)
                    => ReservedPropertyKind.LastTaskResult,

                // MSBuildProgramFiles32
                'P' or 'p' when MatchesAt(propertyName, "ProgramFiles32", startIndex + PrefixLength, endIndex)
                    => ReservedPropertyKind.ProgramFiles32,

                _ => ReservedPropertyKind.None,
            },

            // MSBuildAssemblyVersion
            22 when MatchesAt(propertyName, "AssemblyVersion", startIndex + PrefixLength, endIndex)
                => ReservedPropertyKind.AssemblyVersion,

            // MSBuildStartupDirectory
            23 when MatchesAt(propertyName, "StartupDirectory", startIndex + PrefixLength, endIndex)
                => ReservedPropertyKind.StartupDirectory,

            // MSBuildDisableFeaturesFromVersion
            33 when MatchesAt(propertyName, "DisableFeaturesFromVersion", startIndex + PrefixLength, endIndex)
                => ReservedPropertyKind.DisableFeaturesFromVersion,

            _ => ReservedPropertyKind.None,
        };

        return result is not ReservedPropertyKind.None;

        static ReservedPropertyKind GetProjectPropertyKind(string propertyName, int startIndex, int length)
        {
            const int ProjectPrefixLength = 14; // ProjectPrefix.Length

            int endIndex = startIndex + length;
            int suffixStartIndex = startIndex + ProjectPrefixLength;

            return length switch
            {
                18 => propertyName[suffixStartIndex] switch
                {
                    // MSBuildProjectFile
                    'F' or 'f' when MatchesAt(propertyName, "File", suffixStartIndex, endIndex)
                        => ReservedPropertyKind.ProjectFile,

                    // MSBuildProjectName
                    'N' or 'n' when MatchesAt(propertyName, "Name", suffixStartIndex, endIndex)
                        => ReservedPropertyKind.ProjectName,

                    _ => ReservedPropertyKind.None,
                },

                // MSBuildProjectFullPath
                22 when MatchesAt(propertyName, "FullPath", suffixStartIndex, endIndex)
                    => ReservedPropertyKind.ProjectFullPath,

                23 => propertyName[suffixStartIndex] switch
                {
                    // MSBuildProjectDirectory
                    'D' or 'd' when MatchesAt(propertyName, "Directory", suffixStartIndex, endIndex)
                        => ReservedPropertyKind.ProjectDirectory,

                    // MSBuildProjectExtension
                    'E' or 'e' when MatchesAt(propertyName, "Extension", suffixStartIndex, endIndex)
                        => ReservedPropertyKind.ProjectExtension,

                    _ => ReservedPropertyKind.None,
                },

                // MSBuildProjectDefaultTargets
                28 when MatchesAt(propertyName, "DefaultTargets", suffixStartIndex, endIndex)
                    => ReservedPropertyKind.ProjectDefaultTargets,

                // MSBuildProjectDirectoryNoRoot
                29 when MatchesAt(propertyName, "DirectoryNoRoot", suffixStartIndex, endIndex)
                    => ReservedPropertyKind.ProjectDirectoryNoRoot,

                _ => ReservedPropertyKind.None,
            };
        }

        static ReservedPropertyKind GetThisFilePropertyKind(string propertyName, int startIndex, int length)
        {
            const int ThisFilePrefixLength = 15; // ThisFilePrefix.Length

            int endIndex = startIndex + length;
            int suffixStartIndex = startIndex + ThisFilePrefixLength;

            return length switch
            {
                // MSBuildThisFile
                15 => ReservedPropertyKind.ThisFile,

                // MSBuildThisFileName
                19 when MatchesAt(propertyName, "Name", suffixStartIndex, endIndex)
                    => ReservedPropertyKind.ThisFileName,

                // MSBuildThisFileFullPath
                23 when MatchesAt(propertyName, "FullPath", suffixStartIndex, endIndex)
                    => ReservedPropertyKind.ThisFileFullPath,

                24 => propertyName[suffixStartIndex] switch
                {
                    // MSBuildThisFileDirectory
                    'D' or 'd' when MatchesAt(propertyName, "Directory", suffixStartIndex, endIndex)
                        => ReservedPropertyKind.ThisFileDirectory,

                    // MSBuildThisFileExtension
                    'E' or 'e' when MatchesAt(propertyName, "Extension", suffixStartIndex, endIndex)
                        => ReservedPropertyKind.ThisFileExtension,

                    _ => ReservedPropertyKind.None,
                },

                // MSBuildThisFileDirectoryNoRoot
                30 when MatchesAt(propertyName, "DirectoryNoRoot", suffixStartIndex, endIndex)
                    => ReservedPropertyKind.ThisFileDirectoryNoRoot,

                _ => ReservedPropertyKind.None,
            };
        }

        static bool MatchesAt(string propertyName, string expected, int startIndex, int endIndex)
            => endIndex >= startIndex + expected.Length
                && MSBuildNameIgnoreCaseComparer.Default.Equals(expected, propertyName, startIndex, expected.Length);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Internal;

/// <summary>
///  Identifies a reserved MSBuild property.
/// </summary>
internal enum ReservedPropertyKind
{
    /// <summary>
    ///  Does not identify a reserved property.
    /// </summary>
    None = 0,

    /// <summary>
    ///  Identifies <c>MSBuildProjectDirectory</c>, the directory containing the project.
    /// </summary>
    ProjectDirectory,

    /// <summary>
    ///  Identifies <c>MSBuildProjectDirectoryNoRoot</c>, the project directory without its root.
    /// </summary>
    ProjectDirectoryNoRoot,

    /// <summary>
    ///  Identifies <c>MSBuildProjectFile</c>, the project file name including its extension.
    /// </summary>
    ProjectFile,

    /// <summary>
    ///  Identifies <c>MSBuildProjectExtension</c>, the project file extension.
    /// </summary>
    ProjectExtension,

    /// <summary>
    ///  Identifies <c>MSBuildProjectFullPath</c>, the full path to the project file.
    /// </summary>
    ProjectFullPath,

    /// <summary>
    ///  Identifies <c>MSBuildProjectName</c>, the project file name without its extension.
    /// </summary>
    ProjectName,

    /// <summary>
    ///  Identifies <c>MSBuildThisFileDirectory</c>, the directory containing the file being evaluated.
    /// </summary>
    ThisFileDirectory,

    /// <summary>
    ///  Identifies <c>MSBuildThisFileDirectoryNoRoot</c>, the current file's directory without its root.
    /// </summary>
    ThisFileDirectoryNoRoot,

    /// <summary>
    ///  Identifies <c>MSBuildThisFile</c>, the current file name including its extension.
    /// </summary>
    ThisFile,

    /// <summary>
    ///  Identifies <c>MSBuildThisFileExtension</c>, the current file extension.
    /// </summary>
    ThisFileExtension,

    /// <summary>
    ///  Identifies <c>MSBuildThisFileFullPath</c>, the full path to the file being evaluated.
    /// </summary>
    ThisFileFullPath,

    /// <summary>
    ///  Identifies <c>MSBuildThisFileName</c>, the current file name without its extension.
    /// </summary>
    ThisFileName,

    /// <summary>
    ///  Identifies <c>MSBuildBinPath</c>, the directory containing the MSBuild binaries.
    /// </summary>
    BinPath,

    /// <summary>
    ///  Identifies <c>MSBuildProjectDefaultTargets</c>, the project's default targets.
    /// </summary>
    ProjectDefaultTargets,

    /// <summary>
    ///  Identifies <c>MSBuildToolsPath</c>, the path to the active MSBuild toolset.
    /// </summary>
    ToolsPath,

    /// <summary>
    ///  Identifies <c>MSBuildToolsVersion</c>, the version of the active MSBuild toolset.
    /// </summary>
    ToolsVersion,

    /// <summary>
    ///  Identifies <c>MSBuildRuntimeType</c>, the runtime on which MSBuild is executing.
    /// </summary>
    RuntimeType,

    /// <summary>
    ///  Identifies <c>MSBuildStartupDirectory</c>, the directory from which MSBuild was started.
    /// </summary>
    StartupDirectory,

    /// <summary>
    ///  Identifies <c>MSBuildNodeCount</c>, the maximum number of build nodes.
    /// </summary>
    BuildNodeCount,

    /// <summary>
    ///  Identifies <c>MSBuildLastTaskResult</c>, whether the preceding task succeeded.
    /// </summary>
    LastTaskResult,

    /// <summary>
    ///  Identifies <c>MSBuildProgramFiles32</c>, the 32-bit Program Files directory.
    /// </summary>
    ProgramFiles32,

    /// <summary>
    ///  Identifies <c>MSBuildAssemblyVersion</c>, the version of the MSBuild assembly.
    /// </summary>
    AssemblyVersion,

    /// <summary>
    ///  Identifies <c>MSBuildVersion</c>, the current MSBuild version.
    /// </summary>
    Version,

    /// <summary>
    ///  Identifies <c>MSBuildInteractive</c>, whether MSBuild is running interactively.
    /// </summary>
    Interactive,

    /// <summary>
    ///  Identifies <c>MSBuildDisableFeaturesFromVersion</c>, the version from which features are disabled.
    /// </summary>
    DisableFeaturesFromVersion,
}

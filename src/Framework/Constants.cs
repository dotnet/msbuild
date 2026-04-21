// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Constants used solution wide.
    /// </summary>
    internal static class Constants
    {
        /// <summary>
        /// Defines the name of dotnet host path environment variable (e.g  DOTNET_HOST_PATH = C:\msbuild\.dotnet\dotnet.exe).
        /// </summary>
        internal const string DotnetHostPathEnvVarName = "DOTNET_HOST_PATH";

        /// <summary>
        /// The project property name used to get the path to the MSBuild assembly.
        /// </summary>
        internal const string RuntimeIdentifierGraphPath = nameof(RuntimeIdentifierGraphPath);

        /// <summary>
        /// The project property name used to get the root of the .NET Core SDK.
        /// </summary>
        internal const string NetCoreSdkRoot = nameof(NetCoreSdkRoot);

        /// <summary>
        /// Defines the name of dotnet process based on the operating system.
        /// </summary>
        internal static readonly string DotnetProcessName = NativeMethods.IsWindows ? "dotnet.exe" : "dotnet";

        /// <summary>
        /// Defines the name of MSBuild assembly.
        /// </summary>
        internal const string MSBuildAssemblyName = "MSBuild.dll";

        /// <summary>
        /// Defines the name of MSBuild application name.
        /// </summary>
        internal const string MSBuildAppName = "MSBuild";

        /// <summary>
        /// Defines the name of MSBuild executable name based on the operating system.
        /// </summary>
        internal static readonly string MSBuildExecutableName = NativeMethods.IsWindows ? $"{MSBuildAppName}.exe" : MSBuildAppName;

        /// <summary>
        /// If no default tools version is specified in the config file or registry, we'll use 2.0.
        /// The engine will use its binpath for the matching toolset path.
        /// </summary>
        internal const string defaultToolsVersion = "2.0";

        /// <summary>
        /// The toolsversion we will fall back to as a last resort if the default one cannot be found, this fallback should be the most current toolsversion known
        /// </summary>
        internal static string defaultFallbackToolsVersion = MSBuildConstants.CurrentToolsVersion;

        /// <summary>
        /// The toolsversion we will use when we construct the solution wrapper metaprojects; this should be the most current toolsversion known
        /// </summary>
        internal static string defaultSolutionWrapperProjectToolsVersion = MSBuildConstants.CurrentToolsVersion;

        /// <summary>
        /// Name of the property used to specify a Visual Studio version.
        /// </summary>
        internal const string VisualStudioVersionPropertyName = "VisualStudioVersion";

        /// <summary>
        /// Name of the property used to select which sub-toolset to use.
        /// </summary>
        internal const string SubToolsetVersionPropertyName = VisualStudioVersionPropertyName;

        /// <summary>
        /// The constant for the storing full path to the resolved dotnet.
        /// </summary>
        internal const string DotnetHostPath = nameof(DotnetHostPath);

        /// <summary>
        /// The constant for the storing the relative path to MSBuild assembly.
        /// </summary>
        internal const string MSBuildAssemblyPath = nameof(MSBuildAssemblyPath);

        /// <summary>
        /// Current version of this MSBuild Engine assembly in the
        /// form, e.g, "4.0"
        /// </summary>
        internal static string AssemblyVersion => MSBuildConstants.CurrentProductVersion;

        // Name of the environment variable that always points to 32-bit program files.
        internal const string programFilesx86 = "ProgramFiles(x86)";

        internal const string MSBuildAllProjectsPropertyName = "MSBuildAllProjects";

        internal const string TaskHostExplicitlyRequested = "TaskHostExplicitlyRequested";
    }
}

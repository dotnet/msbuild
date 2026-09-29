// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Shared;
using SharedDotNetFrameworkArchitecture = Microsoft.Build.Shared.DotNetFrameworkArchitecture;
using UtilitiesDotNetFrameworkArchitecture = Microsoft.Build.Utilities.DotNetFrameworkArchitecture;

#nullable disable

namespace Microsoft.Build.Utilities;

public static partial class ToolLocationHelper
{
    /// <summary>
    /// Gets the fully qualified path to the system directory i.e. %SystemRoot%\System32
    /// </summary>
    /// <returns>The system path.</returns>
    public static string PathToSystem
        => Environment.GetFolderPath(Environment.SpecialFolder.System);

    /// <summary>
    /// Returns the prefix of the .NET Framework version folder (e.g. "v2.0")
    /// </summary>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <returns></returns>
    public static string GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion version)
        => FrameworkLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersionToSystemVersion(version));

    /// <summary>
    /// Returns the full name of the .NET Framework root registry key
    /// </summary>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <returns></returns>
    public static string GetDotNetFrameworkRootRegistryKey(TargetDotNetFrameworkVersion version)
        => FrameworkLocationHelper.fullDotNetFrameworkRegistryKey;

    /// <summary>
    /// Returns the full name of the .NET Framework SDK root registry key.  When targeting .NET 3.5 or
    /// above, looks in the locations associated with Visual Studio 2010.  If you wish to target the
    /// .NET Framework SDK that ships with Visual Studio Dev11 or later, please use the override that
    /// specifies a VisualStudioVersion.
    /// </summary>
    /// <param name="version">Version of the targeted .NET Framework</param>
    public static string GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion version)
        => GetDotNetFrameworkSdkRootRegistryKey(version, VisualStudioVersion.VersionLatest);

    /// <summary>
    /// Returns the full name of the .NET Framework SDK root registry key
    /// </summary>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <param name="visualStudioVersion">Version of Visual Studio the requested SDK is associated with</param>
    public static string GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion version, VisualStudioVersion visualStudioVersion)
    {
        var dotNetFrameworkVersion = TargetDotNetFrameworkVersionToSystemVersion(version);
        var vsVersion = VisualStudioVersionToSystemVersion(visualStudioVersion);
        return FrameworkLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(dotNetFrameworkVersion, vsVersion);
    }

    /// <summary>
    /// Name of the value of GetDotNetFrameworkRootRegistryKey that contains the SDK install root path. When
    /// targeting .NET 3.5 or above, looks in the locations associated with Visual Studio 2010.  If you wish
    /// to target the .NET Framework SDK that ships with Visual Studio Dev11 or later, please use the override
    /// that specifies a VisualStudioVersion.
    /// </summary>
    /// <param name="version">Version of the targeted .NET Framework</param>
    public static string GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion version)
        => GetDotNetFrameworkSdkInstallKeyValue(version, VisualStudioVersion.VersionLatest);

    /// <summary>
    /// Name of the value of GetDotNetFrameworkRootRegistryKey that contains the SDK install root path
    /// </summary>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <param name="visualStudioVersion">Version of Visual Studio the requested SDK is associated with</param>
    public static string GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion version, VisualStudioVersion visualStudioVersion)
    {
        var dotNetFrameworkVersion = TargetDotNetFrameworkVersionToSystemVersion(version);
        var vsVersion = VisualStudioVersionToSystemVersion(visualStudioVersion);
        return FrameworkLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(dotNetFrameworkVersion, vsVersion);
    }

    /// <summary>
    /// Get a fully qualified path to the frameworks root directory.
    /// </summary>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <returns>Will return 'null' if there is no target frameworks on this machine.</returns>
    public static string GetPathToDotNetFramework(TargetDotNetFrameworkVersion version)
        => GetPathToDotNetFramework(version, UtilitiesDotNetFrameworkArchitecture.Current);

    /// <summary>
    /// Get a fully qualified path to the framework's root directory.
    /// </summary>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <param name="architecture">Desired architecture, or DotNetFrameworkArchitecture.Current for the architecture this process is currently running under.</param>
    /// <returns></returns>
    public static string GetPathToDotNetFramework(TargetDotNetFrameworkVersion version, UtilitiesDotNetFrameworkArchitecture architecture)
    {
        Version frameworkVersion = TargetDotNetFrameworkVersionToSystemVersion(version);
        SharedDotNetFrameworkArchitecture sharedArchitecture = ConvertToSharedDotNetFrameworkArchitecture(architecture);
        return FrameworkLocationHelper.GetPathToDotNetFramework(frameworkVersion, sharedArchitecture);
    }

    /// <summary>
    /// Returns the path to the "bin" directory of the latest .NET Framework SDK. When targeting .NET 3.5
    /// or above, looks in the locations associated with Visual Studio 2010.  If you wish to target
    /// the .NET Framework SDK that ships with Visual Studio Dev11 or later, please use the override
    /// that specifies a VisualStudioVersion.
    /// </summary>
    /// <returns>Path string.</returns>
    public static string GetPathToDotNetFrameworkSdk()
        => GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Latest);

    /// <summary>
    /// Returns the path to the "bin" directory of the .NET Framework SDK. When targeting .NET 3.5
    /// or above, looks in the locations associated with Visual Studio 2010.  If you wish to target
    /// the .NET Framework SDK that ships with Visual Studio Dev11 or later, please use the override
    /// that specifies a VisualStudioVersion.
    /// </summary>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <returns>Path string.</returns>
    public static string GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion version)
        => GetPathToDotNetFrameworkSdk(version, VisualStudioVersion.VersionLatest);

    /// <summary>
    /// Returns the path to the .NET Framework SDK.
    /// </summary>
    /// <param name="version">The <see cref="TargetDotNetFrameworkVersion"/> of the .NET Framework.</param>
    /// <param name="visualStudioVersion">The <see cref="VisualStudioVersion"/> of Visual Studio.</param>
    /// <returns></returns>
    public static string GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion version, VisualStudioVersion visualStudioVersion)
    {
        var dotNetFrameworkVersion = TargetDotNetFrameworkVersionToSystemVersion(version);
        var vsVersion = VisualStudioVersionToSystemVersion(visualStudioVersion);
        return FrameworkLocationHelper.GetPathToDotNetFrameworkSdk(dotNetFrameworkVersion, vsVersion);
    }

    /// <summary>
    /// Returns the path to the reference assemblies location for the given framework version.
    /// </summary>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <returns>Path string.</returns>
    public static string GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion version)
        => FrameworkLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersionToSystemVersion(version));

    /// <summary>
    /// Returns the path to the "bin" directory of the .NET Framework SDK.
    /// </summary>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <param name="visualStudioVersion">Version of Visual Studio the requested SDK is associated with</param>
    /// <returns>Path string.</returns>
    private static string GetPathToDotNetFrameworkSdkToolsFolderRoot(TargetDotNetFrameworkVersion version, VisualStudioVersion visualStudioVersion)
    {
        var dotNetFrameworkVersion = TargetDotNetFrameworkVersionToSystemVersion(version);
        var vsVersion = VisualStudioVersionToSystemVersion(visualStudioVersion);
        return FrameworkLocationHelper.GetPathToDotNetFrameworkSdkTools(dotNetFrameworkVersion, vsVersion);
    }

    private static Version TargetDotNetFrameworkVersionToSystemVersion(TargetDotNetFrameworkVersion version)
    {
        switch (version)
        {
            case TargetDotNetFrameworkVersion.Version11:
                return FrameworkLocationHelper.dotNetFrameworkVersion11;

            case TargetDotNetFrameworkVersion.Version20:
                return FrameworkLocationHelper.dotNetFrameworkVersion20;

            case TargetDotNetFrameworkVersion.Version30:
                return FrameworkLocationHelper.dotNetFrameworkVersion30;

            case TargetDotNetFrameworkVersion.Version35:
                return FrameworkLocationHelper.dotNetFrameworkVersion35;

            case TargetDotNetFrameworkVersion.Version40:
                return FrameworkLocationHelper.dotNetFrameworkVersion40;

            case TargetDotNetFrameworkVersion.Version45:
                return FrameworkLocationHelper.dotNetFrameworkVersion45;

            case TargetDotNetFrameworkVersion.Version451:
                return FrameworkLocationHelper.dotNetFrameworkVersion451;

            case TargetDotNetFrameworkVersion.Version452:
                return FrameworkLocationHelper.dotNetFrameworkVersion452;

            case TargetDotNetFrameworkVersion.Version46:
                return FrameworkLocationHelper.dotNetFrameworkVersion46;

            case TargetDotNetFrameworkVersion.Version461:
                return FrameworkLocationHelper.dotNetFrameworkVersion461;

            case TargetDotNetFrameworkVersion.Version462:
                return FrameworkLocationHelper.dotNetFrameworkVersion462;

            case TargetDotNetFrameworkVersion.Version47:
                return FrameworkLocationHelper.dotNetFrameworkVersion47;

            case TargetDotNetFrameworkVersion.Version471:
                return FrameworkLocationHelper.dotNetFrameworkVersion471;

            case TargetDotNetFrameworkVersion.Version472:
                return FrameworkLocationHelper.dotNetFrameworkVersion472;

            case TargetDotNetFrameworkVersion.Version48:
                return FrameworkLocationHelper.dotNetFrameworkVersion48;

            case TargetDotNetFrameworkVersion.Version481:
            case TargetDotNetFrameworkVersion.Latest: // Latest is a special value to indicate the highest version we know about.
                return FrameworkLocationHelper.dotNetFrameworkVersion481;

            default:
                ErrorUtilities.ThrowArgument("ToolLocationHelper.UnsupportedFrameworkVersion", version);
                return null;
        }
    }

    private static Version VisualStudioVersionToSystemVersion(VisualStudioVersion version)
    {
        return version switch
        {
            VisualStudioVersion.Version100 => FrameworkLocationHelper.visualStudioVersion100,
            VisualStudioVersion.Version110 => FrameworkLocationHelper.visualStudioVersion110,
            VisualStudioVersion.Version120 => FrameworkLocationHelper.visualStudioVersion120,
            VisualStudioVersion.Version140 => FrameworkLocationHelper.visualStudioVersion140,
            VisualStudioVersion.Version150 => FrameworkLocationHelper.visualStudioVersion150,
            VisualStudioVersion.Version160 => FrameworkLocationHelper.visualStudioVersion160,
            VisualStudioVersion.Version170 => FrameworkLocationHelper.visualStudioVersion170,
            VisualStudioVersion.Version180 => FrameworkLocationHelper.visualStudioVersion180,
            _ => Unsupported()
        };

        Version Unsupported()
        {
            ErrorUtilities.ThrowArgument("ToolLocationHelper.UnsupportedVisualStudioVersion", version);
            return null;
        }
    }

    /// <summary>
    /// Get a fully qualified path to a file in the latest .NET Framework SDK. Error if the .NET Framework SDK can't be found.
    /// When targeting .NET 3.5 or above, looks in the locations associated with Visual Studio 2010.  If you wish to
    /// target the .NET Framework SDK that ships with Visual Studio Dev11 or later, please use the override that
    /// specifies a VisualStudioVersion.
    /// </summary>
    /// <param name="fileName">File name to locate in the .NET Framework SDK directory</param>
    /// <returns>Path string.</returns>
    public static string GetPathToDotNetFrameworkSdkFile(string fileName)
        => GetPathToDotNetFrameworkSdkFile(fileName, TargetDotNetFrameworkVersion.Latest);

    /// <summary>
    /// Get a fully qualified path to a file in the .NET Framework SDK. Error if the .NET Framework SDK can't be found.
    /// When targeting .NET 3.5 or above, looks in the locations associated with Visual Studio 2010.  If you wish to
    /// target the .NET Framework SDK that ships with Visual Studio Dev11 or later, please use the override that
    /// specifies a VisualStudioVersion.
    /// </summary>
    /// <param name="fileName">File name to locate in the .NET Framework SDK directory</param>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <returns>Path string.</returns>
    public static string GetPathToDotNetFrameworkSdkFile(string fileName, TargetDotNetFrameworkVersion version)
        => GetPathToDotNetFrameworkSdkFile(fileName, version, VisualStudioVersion.VersionLatest);

    /// <summary>
    /// Get a fully qualified path to a file in the .NET Framework SDK. Error if the .NET Framework SDK can't be found.
    /// </summary>
    /// <param name="fileName">File name to locate in the .NET Framework SDK directory</param>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <param name="visualStudioVersion">Version of Visual Studio the requested SDK is associated with</param>
    /// <returns>Path string.</returns>
    public static string GetPathToDotNetFrameworkSdkFile(string fileName, TargetDotNetFrameworkVersion version, VisualStudioVersion visualStudioVersion)
        => GetPathToDotNetFrameworkSdkFile(
            fileName,
            version,
            visualStudioVersion,
            UtilitiesDotNetFrameworkArchitecture.Current,
            canFallBackIfNecessary: true); /* If the file is not found for the current architecture, it's OK to follow fallback mechanisms. */

    /// <summary>
    /// Get a fully qualified path to a file in the .NET Framework SDK. Error if the .NET Framework SDK can't be found.
    /// </summary>
    /// <param name="fileName">File name to locate in the .NET Framework SDK directory</param>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <param name="architecture">The required architecture of the requested file.</param>
    /// <returns>Path string.</returns>
    public static string GetPathToDotNetFrameworkSdkFile(string fileName, TargetDotNetFrameworkVersion version, UtilitiesDotNetFrameworkArchitecture architecture)
        => GetPathToDotNetFrameworkSdkFile(fileName, version, VisualStudioVersion.VersionLatest, architecture);

    /// <summary>
    /// Get a fully qualified path to a file in the .NET Framework SDK. Error if the .NET Framework SDK can't be found.
    /// </summary>
    /// <param name="fileName">File name to locate in the .NET Framework SDK directory</param>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <param name="visualStudioVersion">Version of Visual Studio</param>
    /// <param name="architecture">The required architecture of the requested file.</param>
    /// <returns>Path string.</returns>
    public static string GetPathToDotNetFrameworkSdkFile(string fileName, TargetDotNetFrameworkVersion version, VisualStudioVersion visualStudioVersion, UtilitiesDotNetFrameworkArchitecture architecture)
        => GetPathToDotNetFrameworkSdkFile(
            fileName,
            version,
            visualStudioVersion,
            architecture,
            false); /* Do _not_ fall back -- if the user is specifically requesting a particular architecture, they want that architecture. */

    /// <summary>
    /// Get a fully qualified path to a file in the .NET Framework SDK. Error if the .NET Framework SDK can't be found.
    /// </summary>
    /// <param name="fileName">File name to locate in the .NET Framework SDK directory</param>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <param name="visualStudioVersion">Version of Visual Studio</param>
    /// <param name="architecture">The required architecture of the requested file.</param>
    /// <param name="canFallBackIfNecessary">If true, will follow the fallback pattern -- from requested architecture, to
    /// current architecture, to x86.  Otherwise, if the requested architecture path doesn't exist, that's it -- no path
    /// will be returned.</param>
    /// <returns></returns>
    private static string GetPathToDotNetFrameworkSdkFile(string fileName, TargetDotNetFrameworkVersion version, VisualStudioVersion visualStudioVersion, UtilitiesDotNetFrameworkArchitecture architecture, bool canFallBackIfNecessary)
    {
        string pathToSdk = GetPathToDotNetFrameworkSdkToolsFolderRoot(version, visualStudioVersion);
        string filePath = null;

        if (pathToSdk != null)
        {
            string convertedArchitecture = ConvertDotNetFrameworkArchitectureToProcessorArchitecture(architecture);

            // first take a look at the requested architecture
            filePath = GetPathToDotNetFrameworkSdkFile(fileName, pathToSdk, convertedArchitecture);

            if (filePath == null && canFallBackIfNecessary)
            {
                // Now look for a version of the tool which matches the bitness of this process if we haven't already
                if (!string.Equals(ProcessorArchitecture.CurrentProcessArchitecture, convertedArchitecture, StringComparison.OrdinalIgnoreCase))
                {
                    filePath = GetPathToDotNetFrameworkSdkFile(fileName, pathToSdk, ProcessorArchitecture.CurrentProcessArchitecture);
                }

                // If we couldn't find that and we're in a non-x86 process, then fall back to the x86 version
                if (filePath == null && !string.Equals(ProcessorArchitecture.X86, ProcessorArchitecture.CurrentProcessArchitecture, StringComparison.OrdinalIgnoreCase))
                {
                    filePath = GetPathToDotNetFrameworkSdkFile(fileName, pathToSdk, ProcessorArchitecture.X86);
                }
            }
        }

        return filePath;
    }

    /// <summary>
    /// Gets the path to a sdk exe based on the processor architecture and the provided bin directory path.
    /// If the fileName cannot be found in the pathToSDK after the processor architecture has been taken into account a null is returned.
    /// </summary>
    internal static string GetPathToDotNetFrameworkSdkFile(string fileName, string pathToSdk, string processorArchitecture)
    {
        if (pathToSdk == null || fileName == null || processorArchitecture == null)
        {
            return null;
        }

        switch (processorArchitecture)
        {
            case ProcessorArchitecture.AMD64:
                pathToSdk = Path.Combine(pathToSdk, "x64");
                break;

            case ProcessorArchitecture.IA64:
                pathToSdk = Path.Combine(pathToSdk, "ia64");
                break;

            case ProcessorArchitecture.X86:
            case ProcessorArchitecture.ARM:
            default:
                break;
        }

        string filePath = Path.Combine(pathToSdk, fileName);

        // Use FileInfo instead of FileSystems.Default.FileExists(...) because the latter fails silently (by design) if CAS
        // doesn't grant access. We want the security exception if there is going to be one.
        bool exists = new FileInfo(filePath).Exists;
        if (!exists)
        {
            return null;
        }

        return filePath;
    }

    /// <summary>
    /// Given a member of the DotNetFrameworkArchitecture enumeration, returns the equivalent ProcessorArchitecture string.
    /// Internal for Testing Purposes Only
    /// </summary>
    /// <param name="architecture"></param>
    /// <returns></returns>
    internal static string ConvertDotNetFrameworkArchitectureToProcessorArchitecture(DotNetFrameworkArchitecture architecture)
        => architecture switch
        {
            DotNetFrameworkArchitecture.Bitness32
                => ProcessorArchitecture.CurrentProcessArchitecture is ProcessorArchitecture.ARM or ProcessorArchitecture.ARM64
                    ? ProcessorArchitecture.ARM
                    : ProcessorArchitecture.X86,
            DotNetFrameworkArchitecture.Bitness64
                => NativeMethodsShared.ProcessorArchitectureNative switch
                {
                    NativeMethodsShared.ProcessorArchitectures.X64 => ProcessorArchitecture.AMD64,
                    NativeMethodsShared.ProcessorArchitectures.IA64 => ProcessorArchitecture.IA64,
                    NativeMethodsShared.ProcessorArchitectures.ARM64 => ProcessorArchitecture.ARM64,

                    // Error, OK, we're trying to get the 64-bit path on a 32-bit machine.
                    // That ... doesn't make sense.
                    NativeMethodsShared.ProcessorArchitectures.X86 => null,
                    NativeMethodsShared.ProcessorArchitectures.ARM => null,

                    // unknown architecture? return null
                    _ => null,
                },
            DotNetFrameworkArchitecture.Current => ProcessorArchitecture.CurrentProcessArchitecture,
            _ => Assumed.Unreachable<string>(),
        };

    /// <summary>
    /// Returns the path to the Windows SDK for the desired .NET Framework and Visual Studio version.  Note that
    /// this is only supported for a targeted .NET Framework version of 4.5 and above.
    /// </summary>
    /// <param name="version">Target .NET Framework version</param>
    /// <param name="visualStudioVersion">Version of Visual Studio associated with the SDK.</param>
    /// <returns>Path to the appropriate Windows SDK location</returns>
    [Obsolete("Consider using GetPlatformSDKLocation instead")]
    public static string GetPathToWindowsSdk(TargetDotNetFrameworkVersion version, VisualStudioVersion visualStudioVersion)
        => FrameworkLocationHelper.GetPathToWindowsSdk(TargetDotNetFrameworkVersionToSystemVersion(version));

    /// <summary>
    /// Returns the path to a file in the Windows SDK for the desired .NET Framework and Visual Studio version.  Note that
    /// this is only supported for a targeted .NET Framework version of 4.5 and above.
    /// </summary>
    /// <param name="fileName">The name of the file being requested.</param>
    /// <param name="version">Target .NET Framework version.</param>
    /// <param name="visualStudioVersion">Version of Visual Studio associated with the SDK.</param>
    /// <returns>Path to the appropriate Windows SDK file</returns>
    [Obsolete("Consider using GetPlatformSDKLocationFile instead")]
    public static string GetPathToWindowsSdkFile(string fileName, TargetDotNetFrameworkVersion version, VisualStudioVersion visualStudioVersion)
        => GetPathToWindowsSdkFile(
            fileName,
            version,
            visualStudioVersion,
            UtilitiesDotNetFrameworkArchitecture.Current,
            true); /* If the file is not found for the current architecture, it's OK to follow fallback mechanisms. */

    /// <summary>
    /// Returns the path to a file in the Windows SDK for the desired .NET Framework and Visual Studio version and the desired
    /// architecture.  Note that this is only supported for a targeted .NET Framework version of 4.5 and above.
    /// </summary>
    /// <param name="fileName">The name of the file being requested.</param>
    /// <param name="version">Target .NET Framework version.</param>
    /// <param name="visualStudioVersion">Version of Visual Studio associated with the SDK.</param>
    /// <param name="architecture">Desired architecture of the resultant file.</param>
    /// <returns>Path to the appropriate Windows SDK file</returns>
    [Obsolete("Consider using GetPlatformSDKLocationFile instead")]
    public static string GetPathToWindowsSdkFile(string fileName, TargetDotNetFrameworkVersion version, VisualStudioVersion visualStudioVersion, DotNetFrameworkArchitecture architecture)
        => GetPathToWindowsSdkFile(
            fileName,
            version,
            visualStudioVersion,
            architecture,
            false); /* Do _not_ fall back -- if the user is specifically requesting a particular architecture, they want that architecture. */

    /// <summary>
    /// Returns the path to a file in the Windows SDK for the desired .NET Framework and Visual Studio version and the desired
    /// architecture.  Note that this is only supported for a targeted .NET Framework version of 4.5 and above.
    /// </summary>
    /// <param name="fileName">The name of the file being requested.</param>
    /// <param name="version">Target .NET Framework version.</param>
    /// <param name="visualStudioVersion">Version of Visual Studio associated with the SDK.</param>
    /// <param name="architecture">Desired architecture of the resultant file.</param>
    /// <param name="canFallBackIfNecessary"><code>true</code> to fallback, otherwise <code>false</code>.</param>
    /// <returns>Path to the appropriate Windows SDK file</returns>
    [Obsolete("Consider using GetPlatformSDKLocationFile instead")]
    private static string GetPathToWindowsSdkFile(string fileName, TargetDotNetFrameworkVersion version, VisualStudioVersion visualStudioVersion, DotNetFrameworkArchitecture architecture, bool canFallBackIfNecessary)
    {
        string pathToSdk = GetPathToWindowsSdk(version, visualStudioVersion);
        string filePath = null;

        if (pathToSdk != null)
        {
            pathToSdk = Path.Combine(pathToSdk, "bin");

            string convertedArchitecture = ConvertDotNetFrameworkArchitectureToProcessorArchitecture(architecture);

            // first take a look at the requested architecture
            filePath = GetPathToWindowsSdkFile(fileName, pathToSdk, convertedArchitecture);

            if (filePath == null && canFallBackIfNecessary)
            {
                // Now look for a version of the tool which matches the bitness of this process if we haven't already
                if (!string.Equals(ProcessorArchitecture.CurrentProcessArchitecture, convertedArchitecture, StringComparison.OrdinalIgnoreCase))
                {
                    filePath = GetPathToWindowsSdkFile(fileName, pathToSdk, ProcessorArchitecture.CurrentProcessArchitecture);
                }

                // If we couldn't find that and we're in a non-x86 process, then fall back to the x86 version
                if (filePath == null && !string.Equals(ProcessorArchitecture.X86, ProcessorArchitecture.CurrentProcessArchitecture, StringComparison.OrdinalIgnoreCase))
                {
                    filePath = GetPathToWindowsSdkFile(fileName, pathToSdk, ProcessorArchitecture.X86);
                }
            }
        }

        return filePath;
    }

    /// <summary>
    /// Gets the path to a sdk exe based on the processor architecture and the provided bin directory path.
    /// If the fileName cannot be found in the pathToSDK after the processor architecture has been taken into account a null is returned.
    /// </summary>
    [Obsolete("Consider using GetPlatformSDKLocationFile instead")]
    private static string GetPathToWindowsSdkFile(string fileName, string pathToSdk, string processorArchitecture)
    {
        if (pathToSdk == null || fileName == null || processorArchitecture == null)
        {
            return null;
        }

        switch (processorArchitecture)
        {
            case ProcessorArchitecture.X86:
                pathToSdk = Path.Combine(pathToSdk, "x86");
                break;

            case ProcessorArchitecture.AMD64:
                pathToSdk = Path.Combine(pathToSdk, "x64");
                break;

            case ProcessorArchitecture.IA64:
            case ProcessorArchitecture.ARM:
            default:
                break;
        }

        string filePath = Path.Combine(pathToSdk, fileName);

        // Use FileInfo instead of FileSystems.Default.FileExists(...) because the latter fails silently (by design) if CAS
        // doesn't grant access. We want the security exception if there is going to be one.
        bool exists = new FileInfo(filePath).Exists;
        if (!exists)
        {
            return null;
        }

        return filePath;
    }

    /// <summary>
    /// Given a ToolsVersion, return the path to the MSBuild tools for that ToolsVersion
    /// </summary>
    /// <param name="toolsVersion">The ToolsVersion for which to get the tools path</param>
    /// <returns>The tools path folder of the appropriate ToolsVersion if it exists, otherwise null.</returns>
    public static string GetPathToBuildTools(string toolsVersion)
        => GetPathToBuildTools(toolsVersion, UtilitiesDotNetFrameworkArchitecture.Current);

    /// <summary>
    /// Given a ToolsVersion, return the path to the MSBuild tools for that ToolsVersion
    /// </summary>
    /// <param name="toolsVersion">The ToolsVersion for which to get the tools path</param>
    /// <param name="architecture">The architecture of the build tools location to get</param>
    /// <returns>The tools path folder of the appropriate ToolsVersion if it exists, otherwise null.</returns>
    public static string GetPathToBuildTools(string toolsVersion, UtilitiesDotNetFrameworkArchitecture architecture)
        => toolsVersion switch
        {
            "2.0" => GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20, architecture),
            "3.5" => GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35, architecture),
            "4.0" => GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40, architecture),

            // Doesn't map to an existing .NET Framework, so let's grab it out of the toolset.
            _ => FrameworkLocationHelper.GeneratePathToBuildToolsForToolsVersion(toolsVersion, ConvertToSharedDotNetFrameworkArchitecture(architecture)),
        };

    /// <summary>
    /// Given the name of a file and a ToolsVersion, return the path to that file in the MSBuild
    /// tools path for that ToolsVersion
    /// </summary>
    /// <param name="fileName">The file to find the path to</param>
    /// <param name="toolsVersion">The ToolsVersion in which to find the file</param>
    /// <returns>The path to the file in the tools path folder of the appropriate ToolsVersion if it
    /// exists, otherwise null.</returns>
    public static string GetPathToBuildToolsFile(string fileName, string toolsVersion)
        => GetPathToBuildToolsFile(fileName, toolsVersion, UtilitiesDotNetFrameworkArchitecture.Current);

    /// <summary>
    /// Given the name of a file and a ToolsVersion, return the path to that file in the MSBuild
    /// tools path for that ToolsVersion
    /// </summary>
    /// <param name="fileName">The file to find the path to</param>
    /// <param name="toolsVersion">The ToolsVersion in which to find the file</param>
    /// <param name="architecture">The architecture of the build tools file to get</param>
    /// <returns>The path to the file in the tools path folder of the appropriate ToolsVersion if it
    /// exists, otherwise null.</returns>
    public static string GetPathToBuildToolsFile(string fileName, string toolsVersion, UtilitiesDotNetFrameworkArchitecture architecture)
    {
        string toolPath = GetPathToBuildTools(toolsVersion, architecture);

        if (toolPath != null)
        {
            toolPath = Path.Combine(toolPath, fileName);

            // Rollback see https://developercommunity.visualstudio.com/t/Unable-to-locate-MSBuild-path-with-Lates/10824132
            if (!File.Exists(toolPath))
            {
                toolPath = null;
            }
        }

        return toolPath;
    }

    /// <summary>
    /// Get a fully qualified path to a file in the frameworks root directory.
    /// </summary>
    /// <param name="fileName">File name to locate in the .NET Framework directory</param>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <returns>Will return 'null' if there is no target frameworks on this machine.</returns>
    public static string GetPathToDotNetFrameworkFile(string fileName, TargetDotNetFrameworkVersion version)
        => GetPathToDotNetFrameworkFile(fileName, version, UtilitiesDotNetFrameworkArchitecture.Current);

    /// <summary>
    /// Get a fully qualified path to a file in the frameworks root directory for the specified architecture.
    /// </summary>
    /// <param name="fileName">File name to locate in the .NET Framework directory</param>
    /// <param name="version">Version of the targeted .NET Framework</param>
    /// <param name="architecture">Desired architecture, or DotNetFrameworkArchitecture.Current for the architecture this process is currently running under.</param>
    /// <returns>Will return 'null' if there is no target frameworks on this machine.</returns>
    public static string GetPathToDotNetFrameworkFile(string fileName, TargetDotNetFrameworkVersion version, UtilitiesDotNetFrameworkArchitecture architecture)
    {
        string pathToFx = GetPathToDotNetFramework(version, architecture);
        return pathToFx == null ? null : Path.Combine(pathToFx, fileName);
    }

    /// <summary>
    /// Get a fully qualified path to a file in the system directory (i.e. %SystemRoot%\System32)
    /// </summary>
    /// <param name="fileName">File name to locate in the system directory</param>
    /// <returns>Path string.</returns>
    public static string GetPathToSystemFile(string fileName) => Path.Combine(PathToSystem, fileName);

    /// <summary>
    /// Converts a member of the Microsoft.Build.Utilities.DotNetFrameworkArchitecture enum to the equivalent member of the
    /// Microsoft.Build.Shared.DotNetFrameworkArchitecture enum.
    /// </summary>
    private static SharedDotNetFrameworkArchitecture ConvertToSharedDotNetFrameworkArchitecture(UtilitiesDotNetFrameworkArchitecture architecture)
        => architecture switch
        {
            UtilitiesDotNetFrameworkArchitecture.Current => SharedDotNetFrameworkArchitecture.Current,
            UtilitiesDotNetFrameworkArchitecture.Bitness32 => SharedDotNetFrameworkArchitecture.Bitness32,
            UtilitiesDotNetFrameworkArchitecture.Bitness64 => SharedDotNetFrameworkArchitecture.Bitness64,
            _ => Assumed.Unreachable<SharedDotNetFrameworkArchitecture>(),
        };
}

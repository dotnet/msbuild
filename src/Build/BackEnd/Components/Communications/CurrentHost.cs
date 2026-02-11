// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if RUNTIME_TYPE_NETCORE
using System.IO;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Constants = Microsoft.Build.Framework.Constants;
#endif

namespace Microsoft.Build.BackEnd;

internal static class CurrentHost
{

#if RUNTIME_TYPE_NETCORE
    private static string? s_currentHost;
#endif

    /// <summary>
    /// Identify the .NET host of the current process.
    /// </summary>
    /// <returns>The full path to the executable hosting the current process, or null if running on Full Framework on Windows.</returns>
    public static string? GetCurrentHost()
    {
#if RUNTIME_TYPE_NETCORE
        if (s_currentHost == null)
        {
            string dotnetExe = Path.Combine(
                FileUtilities.GetFolderAbove(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, 2),
                Constants.DotnetProcessName);
            if (FileSystems.Default.FileExists(dotnetExe))
            {
                s_currentHost = dotnetExe;
            }
            else
            {
                if (EnvironmentUtilities.ProcessPath is string processPath
                    && Path.GetFileName(processPath) == Constants.DotnetProcessName)
                {
                    // If the current process is already running in a general-purpose host, use its path.
                    s_currentHost = processPath;
                }
                else
                {
                    // Otherwise, we don't know the host. Try to infer it from the current runtime, which will be something like
                    // "C:\Program Files\dotnet\shared\Microsoft.NETCore.App\9.0.6\" on Windows.
                    //                     ^4     ^3              ^2          ^1
                    dotnetExe = Path.Combine(
                        FileUtilities.GetFolderAbove(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), 4),
                        Constants.DotnetProcessName);
                    if (FileSystems.Default.FileExists(dotnetExe))
                    {
                        s_currentHost = dotnetExe;
                    }
                    else
                    {
                        ErrorUtilities.ThrowInternalErrorUnreachable();
                    }
                }
            }
        }

        return s_currentHost;
#else
        return null;
#endif
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests.Shared;

public class WindowsNet35OnlyFactAttribute : FactAttribute
{
    private const string Message = "This test only runs on Windows under .NET Framework when .NET Framework 3.5 is installed.";

    public WindowsNet35OnlyFactAttribute(string? additionalMessage = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            !RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase) ||
            !IsNetFramework35Installed() ||
            !BootstrapHasNetFxMicrosoftNetBuildExtensions())
        {
            Skip = SkipMessage(additionalMessage);
        }
    }

    private static string SkipMessage(string? additionalMessage = null)
        => !string.IsNullOrWhiteSpace(additionalMessage) ? $"{Message} {additionalMessage}" : Message;

    private static bool IsNetFramework35Installed()
        => FrameworkLocationHelper.GetPathToDotNetFrameworkV35(DotNetFrameworkArchitecture.Current) != null;

    /// <summary>
    ///  Checks to see if the .NET Framework version of Microsoft.NET.Build.Extensions is installed.
    ///  If it isn't, building building for .NET Framework 3.5 will fail.
    /// </summary>
    private static bool BootstrapHasNetFxMicrosoftNetBuildExtensions()
    {
        var binDir = new DirectoryInfo(RunnerUtilities.BootstrapMsBuildBinaryLocation);

        // The bin directory should be something like, D:\repo\msbuild\artifacts\bin\bootstrap\net472\MSBuild\Current\Bin
        // Walk up three levels to get the TFM folder name.
        // Then, look for .\bootstrap\TFM\MSBuild\Microsoft\Microsoft.NET.Build.Extensions\tools\TFM
        if (binDir == null || !"Bin".Equals(binDir.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var currentDir = binDir.Parent;
        if (currentDir == null || !"Current".Equals(currentDir.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var msbuildDir = currentDir.Parent;
        if (msbuildDir == null || !"MSBuild".Equals(msbuildDir.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var tfmDir = msbuildDir.Parent;
        string? tfm = tfmDir?.Name;

        if (tfm == null || !tfm.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var directories = msbuildDir.GetDirectories(@"Microsoft\Microsoft.NET.Build.Extensions\tools\net4*");

        return Array.Exists(directories, x => x.Name == tfm);
    }
}

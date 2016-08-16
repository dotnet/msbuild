// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    [DebuggerDisplay("DirectoryPath: {DirectoryPath}, TargetFrameworkVersion = {TargetFrameworkVersion}")]
    public class AssemblyFoldersFromConfigInfo
    {
        public AssemblyFoldersFromConfigInfo(string directoryPath, Version targetFrameworkVersion)
        {
            ErrorUtilities.VerifyThrowArgumentNull(directoryPath, "directoryPath");
            ErrorUtilities.VerifyThrowArgumentNull(targetFrameworkVersion, "targetFrameworkVersion");

            // When we get a path, it may be relative to Visual Studio (i.e. reference assemblies). If the
            // VSInstallDir environment is used, replace with our known location to Visual Studio.
            if (!string.IsNullOrEmpty(BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory))
            {
                directoryPath = Regex.Replace(directoryPath, "%VSINSTALLDIR%",
                    BuildEnvironmentHelper.Instance.VisualStudioInstallRootDirectory, RegexOptions.IgnoreCase);
            }

            DirectoryPath = Environment.ExpandEnvironmentVariables(directoryPath);
            TargetFrameworkVersion = targetFrameworkVersion;
        }

        public string DirectoryPath { get; }

        public Version TargetFrameworkVersion { get; }
    }
}
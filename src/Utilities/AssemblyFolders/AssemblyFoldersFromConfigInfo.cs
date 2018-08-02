// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Represents information about assembly folders.
    /// </summary>
    [DebuggerDisplay("DirectoryPath: {DirectoryPath}, TargetFrameworkVersion = {TargetFrameworkVersion}")]
    public class AssemblyFoldersFromConfigInfo
    {
        /// <summary>
        /// Initializes a new instance of the AssemblyFoldersFromConfigInfo class.
        /// </summary>
        /// <param name="directoryPath">The directory path.</param>
        /// <param name="targetFrameworkVersion">The <see cref="Version"/> of the target framework.</param>
        public AssemblyFoldersFromConfigInfo(string directoryPath, Version targetFrameworkVersion)
        {
            ErrorUtilities.VerifyThrowArgumentNull(directoryPath, nameof(directoryPath));
            ErrorUtilities.VerifyThrowArgumentNull(targetFrameworkVersion, nameof(targetFrameworkVersion));

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

        /// <summary>
        /// Gets the path to the assembly folder.
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        /// Gets the <see cref="Version"/> of the target framework.
        /// </summary>
        public Version TargetFrameworkVersion { get; }
    }
}

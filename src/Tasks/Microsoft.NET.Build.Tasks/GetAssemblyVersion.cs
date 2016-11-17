// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Versioning;
using System.Diagnostics;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Determines the assembly version to use for a given semantic version.
    /// </summary>
    public class GetAssemblyVersion : TaskBase
    {
        /// <summary>
        /// The nuget version from which to get an assembly version portion.
        /// </summary>
        [Required]
        public string NuGetVersion { get; set; }

        /// <summary>
        /// The assembly version (major.minor.patch.revision) portion of the nuget version.
        /// </summary>
        [Output]
        public string AssemblyVersion { get; set; }

        protected override void ExecuteCore()
        {
            NuGetVersion nugetVersion;
            if (!NuGet.Versioning.NuGetVersion.TryParse(NuGetVersion, out nugetVersion))
            {
                Log.LogError(Strings.InvalidNuGetVersionString, NuGetVersion);
                return;
            }

            AssemblyVersion = nugetVersion.Version.ToString();
        }
    }
}

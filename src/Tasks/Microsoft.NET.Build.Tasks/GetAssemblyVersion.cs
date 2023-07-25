// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGet.Versioning;

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

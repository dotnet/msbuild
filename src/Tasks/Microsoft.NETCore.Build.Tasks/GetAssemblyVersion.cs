// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Versioning;
using System.Diagnostics;

namespace Microsoft.DotNet.Core.Build.Tasks
{
    /// <summary>
    /// Determines the assembly version to use for a given semantic version.
    /// </summary>
    public class GetAssemblyVersion : Task
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

        public override bool Execute()
        {
            // Using try/catch instead of TryParse so that we don't need to maintain our own error message here.
            try
            {
                AssemblyVersion = NuGet.Versioning.NuGetVersion.Parse(NuGetVersion).Version.ToString();
                return true;
            }
            catch (ArgumentException ex)
            {
                Log.LogError(ex.Message);
                return false;
            }
        }
    }
}

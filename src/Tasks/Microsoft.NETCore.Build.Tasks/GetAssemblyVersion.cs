// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Microsoft.DotNet.Core.Build.Tasks
{
    /// <summary>
    /// Determines the assembly version to use for a given semantic version.
    /// </summary>
    public class GetAssemblyVersion : Task
    {
        /// <summary>
        /// The semantic version from which to get an assembly version portion.
        /// </summary>
        [Required]
        public string SemanticVersion { get; set; }

        /// <summary>
        /// The assembly version (major.minor.patch) portion of the semantic version.
        /// </summary>
        [Output]
        public string AssemblyVersion { get; set; }

        public override bool Execute()
        {
            NuGetVersion version;
            if (string.IsNullOrEmpty(SemanticVersion) || !NuGetVersion.TryParseStrict(SemanticVersion, out version))
            {
                // TODO: Localize. Blocked by https://github.com/dotnet/sdk/issues/33
                Log.LogError($"Invalid semantic version: '{SemanticVersion}'");
                return false;
            }

            AssemblyVersion = new Version(version.Major, version.Minor, version.Patch).ToString();
            return true;
        }
    }
}

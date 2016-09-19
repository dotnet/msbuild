// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Versioning;
using System.Diagnostics;
using NuGet.Frameworks;
using System.Runtime.Versioning;
using System.Linq;

namespace Microsoft.DotNet.Core.Build.Tasks
{
    /// <summary>
    /// Gets the "nearest" framework
    /// </summary>
    public class GetNearestTargetFramework : Task
    {
        /// <summary>
        /// The target framework of the referring project (in short form or full TFM)
        /// </summary>
        [Required]
        public string ReferringTargetFramework { get; set; }

        /// <summary>
        /// The target frameworks supported by the project being referenced (in short form or full TFM).
        /// </summary>
        [Required]
        public string[] PossibleTargetFrameworks { get; set; }

        /// <summary>
        /// The entry in <see cref="PossibleTargetFrameworks"/> that is "nearest" to <see cref="ReferringTargetFramework" />.
        /// If none of the possible frameworks are compatible with the referring framework.
        /// </summary>
        [Output]
        public string NearestTargetFramework { get; private set; }

        public override bool Execute()
        {
            if (PossibleTargetFrameworks.Length < 1)
            {
                // TODO: localize: https://github.com/dotnet/sdk/issues/33
                Log.LogError("At least one possible target framework must be specified.");
                return false;
            }

            var referringNuGetFramework = ParseFramework(ReferringTargetFramework);
            var possibleNuGetFrameworks = PossibleTargetFrameworks.Select(framework => ParseFramework(framework)).ToList(); // ToList() to force enumeration and error logging.

            if (Log.HasLoggedErrors)
            {
                return false;
            }

            var nearestNuGetFramework = new FrameworkReducer().GetNearest(referringNuGetFramework, possibleNuGetFrameworks);
            if (nearestNuGetFramework == null)
            {
                // TODO: localize: https://github.com/dotnet/sdk/issues/33
                Log.LogError($"Project has no no target framework compatible with '{ReferringTargetFramework}'");
            }

            NearestTargetFramework = PossibleTargetFrameworks[possibleNuGetFrameworks.IndexOf(nearestNuGetFramework)];
            return true;
        }

        private NuGetFramework ParseFramework(string name)
        {
            var framework = NuGetFramework.Parse(name);

            if (framework == null)
            {
                // TODO: localize: https://github.com/dotnet/sdk/issues/33
                Log.LogError($"Invalid framework name: '{framework}'.");
            }

            return framework;
        }
    }
}

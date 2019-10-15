// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using NuGet.Frameworks;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Gets the "nearest" framework
    /// </summary>
    public class GetNearestTargetFramework : TaskBase
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
        /// The full path to the project with the given possible target frameworks. 
        /// </summary>
        [Required]
        public string ProjectFilePath { get; set; }

        /// <summary>
        /// The entry in <see cref="PossibleTargetFrameworks"/> that is "nearest" to <see cref="ReferringTargetFramework" />.
        /// If none of the possible frameworks are compatible with the referring framework.
        /// </summary>
        [Output]
        public string NearestTargetFramework { get; private set; }

        protected override void ExecuteCore()
        {
            if (PossibleTargetFrameworks.Length < 1)
            {
                Log.LogError(Strings.AtLeastOneTargetFrameworkMustBeSpecified);
                return;
            }

            var referringNuGetFramework = ParseFramework(ReferringTargetFramework);
            var possibleNuGetFrameworks = PossibleTargetFrameworks.Select(framework => ParseFramework(framework)).ToList(); // ToList() to force enumeration and error logging.

            if (Log.HasLoggedErrors)
            {
                return;
            }

            var nearestNuGetFramework = new FrameworkReducer().GetNearest(referringNuGetFramework, possibleNuGetFrameworks);
            if (nearestNuGetFramework == null)
            {
                Log.LogError(Strings.NoCompatibleTargetFramework, ProjectFilePath, ReferringTargetFramework, string.Join("; ", possibleNuGetFrameworks));
                return;
            }

            // Note that there can be more than one spelling of the same target framework (e.g. net45 and net4.5) and 
            // we must return a value that is spelled exactly the same way as the PossibleTargetFrameworks input. To 
            // achieve this, we find the index of the returned framework among the set we passed to nuget and use that
            // to retrieve a value at the same position in the input.
            //
            // This is required to guarantee that a project can use whatever spelling appears in $(TargetFrameworks)
            // in a condition that compares against $(TargetFramework).
            int indexOfNearestFramework = possibleNuGetFrameworks.IndexOf(nearestNuGetFramework);
            NearestTargetFramework = PossibleTargetFrameworks[indexOfNearestFramework];
        }

        private NuGetFramework ParseFramework(string name)
        {
            var framework = NuGetUtils.ParseFrameworkName(name);

            if (framework == null)
            {
                Log.LogError(Strings.InvalidFrameworkName, framework.ToString());
            }

            return framework;
        }
    }
}

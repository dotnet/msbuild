// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.ProjectJsonMigration;
using NuGet.Frameworks;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools.ProjectExtensions
{
    internal static class ProjectExtensions
    {
        public static IEnumerable<NuGetFramework> GetTargetFrameworks(this Project project)
        {
            var targetFramewoksStrings = project
                    .GetPropertyCommaSeparatedValues("TargetFramework")
                    .Union(project.GetPropertyCommaSeparatedValues("TargetFrameworks"))
                    .Select((value) => value.ToLower());

            var uniqueTargetFrameworkStrings = new HashSet<string>(targetFramewoksStrings);

            return uniqueTargetFrameworkStrings
                .Select((frameworkString) => NuGetFramework.Parse(frameworkString));
        }

        public static IEnumerable<string> GetPropertyCommaSeparatedValues(this Project project, string propertyName)
        {
            return project.GetPropertyValue(propertyName)
                .Split(';')
                .Select((value) => value.Trim())
                .Where((value) => !string.IsNullOrEmpty(value));
        }
    }
}

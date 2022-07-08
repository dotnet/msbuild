// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Sln.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools.Common
{
    public static class ProjectInstanceExtensions
    {
        public static string GetProjectId(this ProjectInstance projectInstance)
        {
            var projectGuidProperty = projectInstance.GetPropertyValue("ProjectGuid");
            var projectGuid = string.IsNullOrEmpty(projectGuidProperty)
                ? Guid.NewGuid()
                : new Guid(projectGuidProperty);
            return projectGuid.ToString("B").ToUpper();
        }

        public static string GetDefaultProjectTypeGuid(this ProjectInstance projectInstance)
        {
            return projectInstance.GetPropertyValue("DefaultProjectTypeGuid");
        }

        public static IEnumerable<string> GetPlatforms(this ProjectInstance projectInstance)
        {
            return (projectInstance.GetPropertyValue("Platforms") ?? "")
                .Split(
                    new char[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .DefaultIfEmpty("AnyCPU");
        }

        public static IEnumerable<string> GetConfigurations(this ProjectInstance projectInstance)
        {
            string foundConfig = projectInstance.GetPropertyValue("Configurations") ?? "Debug;Release";
            if (string.IsNullOrWhiteSpace(foundConfig))
            {
                foundConfig = "Debug;Release";
            }

            return foundConfig
                .Split(
                    new char[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .DefaultIfEmpty("Debug");
        }
    }
}

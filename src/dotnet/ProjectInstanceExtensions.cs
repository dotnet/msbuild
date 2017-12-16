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

        public static string GetProjectTypeGuid(this ProjectInstance projectInstance)
        {
            string projectTypeGuid = null;

            var projectTypeGuidProperty = projectInstance.GetPropertyValue("ProjectTypeGuid");
            if (!string.IsNullOrEmpty(projectTypeGuidProperty))
            {
                projectTypeGuid = projectTypeGuidProperty.Split(';').Last();
            }
            else
            {
                projectTypeGuid = projectInstance.GetPropertyValue("DefaultProjectTypeGuid");
            }

            if (string.IsNullOrEmpty(projectTypeGuid))
            {
                //ISSUE: https://github.com/dotnet/sdk/issues/522
                //The real behavior we want (once DefaultProjectTypeGuid support is in) is to throw
                //when we cannot find ProjectTypeGuid or DefaultProjectTypeGuid. But for now we
                //need to default to the C# one.
                //throw new GracefulException(CommonLocalizableStrings.UnsupportedProjectType);
                projectTypeGuid = ProjectTypeGuids.CSharpProjectTypeGuid;
            }

            return projectTypeGuid;
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
            return (projectInstance.GetPropertyValue("Configurations") ?? "Debug;Release")
                .Split(
                    new char[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .DefaultIfEmpty("Debug");
        }
    }
}

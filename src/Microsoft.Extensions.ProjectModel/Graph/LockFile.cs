// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel.Graph
{
    public class LockFile
    {
        public static readonly int CurrentVersion = 2;
        public static readonly string FileName = "project.lock.json";

        public int Version { get; set; }
        public IList<ProjectFileDependencyGroup> ProjectFileDependencyGroups { get; set; } = new List<ProjectFileDependencyGroup>();
        public IList<LockFilePackageLibrary> PackageLibraries { get; set; } = new List<LockFilePackageLibrary>();
        public IList<LockFileProjectLibrary> ProjectLibraries { get; set; } = new List<LockFileProjectLibrary>();
        public IList<LockFileTarget> Targets { get; set; } = new List<LockFileTarget>();

        public bool IsValidForProject(Project project)
        {
            string message;
            return IsValidForProject(project, out message);
        }

        public bool IsValidForProject(Project project, out string message)
        {
            if (Version != CurrentVersion)
            {
                message = $"The expected lock file version does not match the actual version";
                return false;
            }

            message = $"Dependencies in {Project.FileName} were modified";

            var actualTargetFrameworks = project.GetTargetFrameworks();

            // The lock file should contain dependencies for each framework plus dependencies shared by all frameworks
            if (ProjectFileDependencyGroups.Count != actualTargetFrameworks.Count() + 1)
            {
                return false;
            }

            foreach (var group in ProjectFileDependencyGroups)
            {
                IOrderedEnumerable<string> actualDependencies;
                var expectedDependencies = group.Dependencies.OrderBy(x => x);

                // If the framework name is empty, the associated dependencies are shared by all frameworks
                if (group.FrameworkName == null)
                {
                    actualDependencies = project.Dependencies.Select(RenderDependency).OrderBy(x => x);
                }
                else
                {
                    var framework = actualTargetFrameworks
                        .FirstOrDefault(f => Equals(f.FrameworkName, group.FrameworkName));
                    if (framework == null)
                    {
                        return false;
                    }

                    actualDependencies = framework.Dependencies.Select(RenderDependency).OrderBy(x => x);
                }

                if (!actualDependencies.SequenceEqual(expectedDependencies))
                {
                    return false;
                }
            }

            message = null;
            return true;
        }

        private string RenderDependency(LibraryRange arg)
        {
            var name = arg.Name;

            if (arg.Target == LibraryType.ReferenceAssembly)
            {
                name = $"fx/{name}";
            }

            return $"{name} {RenderVersion(arg.VersionRange)}";
        }

        private string RenderVersion(VersionRange range)
        {
            if (range == null)
            {
                return null;
            }

            if (range.MinVersion == range.MaxVersion &&
                (range.Float == null || range.Float.FloatBehavior == NuGetVersionFloatBehavior.None))
            {
                return range.MinVersion.ToString();
            }
            var sb = new StringBuilder();
            sb.Append(">= ");
            switch (range?.Float?.FloatBehavior)
            {
                case null:
                case NuGetVersionFloatBehavior.None:
                    sb.Append(range.MinVersion);
                    break;
                case NuGetVersionFloatBehavior.Prerelease:
                    // Work around nuget bug: https://github.com/NuGet/Home/issues/1598
                    // sb.AppendFormat("{0}-*", range.MinVersion);
                    sb.Append($"{range.MinVersion.Version.Major}.{range.MinVersion.Version.Minor}.{range.MinVersion.Version.Build}");
                    if (string.IsNullOrEmpty(range.MinVersion.Release) || 
                        string.Equals("-", range.MinVersion.Release))
                    {
                        sb.Append($"-*");
                    }
                    else
                    {
                        sb.Append($"-{range.MinVersion.Release}*");
                    }
                    break;
                case NuGetVersionFloatBehavior.Revision:
                    sb.Append($"{range.MinVersion.Version.Major}.{range.MinVersion.Version.Minor}.{range.MinVersion.Version.Build}.*");
                    break;
                case NuGetVersionFloatBehavior.Patch:
                    sb.Append($"{range.MinVersion.Version.Major}.{range.MinVersion.Version.Minor}.*");
                    break;
                case NuGetVersionFloatBehavior.Minor:
                    sb.AppendFormat($"{range.MinVersion.Version.Major}.*");
                    break;
                case NuGetVersionFloatBehavior.Major:
                    sb.AppendFormat("*");
                    break;
                default:
                    break;
            }

            if (range.MaxVersion != null)
            {
                sb.Append(range.IsMaxInclusive ? " <= " : " < ");
                sb.Append(range.MaxVersion);
            }

            return sb.ToString();
        }
    }
}
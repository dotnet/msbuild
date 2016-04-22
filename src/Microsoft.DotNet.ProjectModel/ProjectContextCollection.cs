// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class ProjectContextCollection
    {
        public Project Project { get; set; }

        public List<ProjectContext> ProjectContexts { get; } = new List<ProjectContext>();

        public IEnumerable<ProjectContext> FrameworkOnlyContexts => ProjectContexts.Where(c => string.IsNullOrEmpty(c.RuntimeIdentifier));

        public List<DiagnosticMessage> ProjectDiagnostics { get; } = new List<DiagnosticMessage>();

        public string LockFilePath { get; set; }

        public string ProjectFilePath { get; set; }

        public DateTime LastProjectFileWriteTimeUtc { get; set; }

        public DateTime LastLockFileWriteTimeUtc { get; set; }

        public bool HasChanged
        {
            get
            {
                if (ProjectFilePath == null || !File.Exists(ProjectFilePath))
                {
                    return true;
                }

                if (LastProjectFileWriteTimeUtc < File.GetLastWriteTimeUtc(ProjectFilePath))
                {
                    return true;
                }

                if (LockFilePath == null || !File.Exists(LockFilePath))
                {
                    return true;
                }

                if (LastLockFileWriteTimeUtc < File.GetLastWriteTimeUtc(LockFilePath))
                {
                    return true;
                }

                return false;
            }
        }

        public ProjectContext GetTarget(NuGetFramework targetFramework) => GetTarget(targetFramework, string.Empty);

        public ProjectContext GetTarget(NuGetFramework targetFramework, string runtimeIdentifier)
        {
            return ProjectContexts
                .FirstOrDefault(c =>
                    Equals(c.TargetFramework, targetFramework) &&
                    string.Equals(c.RuntimeIdentifier ?? string.Empty, runtimeIdentifier ?? string.Empty));
        }

        public void Reset()
        {
            Project = null;
            ProjectContexts.Clear();
            ProjectFilePath = null;
            LockFilePath = null;
            LastLockFileWriteTimeUtc = DateTime.MinValue;
            LastProjectFileWriteTimeUtc = DateTime.MinValue;
            ProjectDiagnostics.Clear();
        }
    }
}

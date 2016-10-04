// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    /// Any changes to this need to be reflected in roslyn-project-system
    /// TODO add link
    public class MigrationReport
    {
        public List<ProjectMigrationReport> ProjectMigrationReports { get; }

        public int MigratedProjectsCount => ProjectMigrationReports.Count;

        public int SucceededProjectsCount => ProjectMigrationReports.Count(p => p.Succeeded);

        public int FailedProjectsCount => ProjectMigrationReports.Count(p => p.Failed);

        public bool AllSucceeded => ! ProjectMigrationReports.Any(p => p.Failed);

        public MigrationReport Merge(MigrationReport otherReport)
        {
            var allReports = ProjectMigrationReports.Concat(otherReport.ProjectMigrationReports).ToList();
            var dedupedReports = DedupeSkippedReports(allReports);

            return new MigrationReport(dedupedReports);
        }

        private List<ProjectMigrationReport> DedupeSkippedReports(List<ProjectMigrationReport> allReports)
        {
            var reportDict = new Dictionary<string, ProjectMigrationReport>();

            foreach (var report in allReports)
            {
                ProjectMigrationReport existingReport;

                if (reportDict.TryGetValue(report.ProjectDirectory, out existingReport))
                {
                    if (existingReport.Skipped)
                    {
                        reportDict[report.ProjectDirectory] = report;
                    }
                    else if (!report.Skipped)
                    {
                        MigrationTrace.Instance.WriteLine("Detected double project migration: {report.ProjectDirectory}");
                    }
                }
                else
                {
                    reportDict[report.ProjectDirectory] = report;
                }
            }

            return reportDict.Values.ToList();
        }

        public MigrationReport(List<ProjectMigrationReport> projectMigrationReports)
        {
            ProjectMigrationReports = projectMigrationReports;
        }
    }
}

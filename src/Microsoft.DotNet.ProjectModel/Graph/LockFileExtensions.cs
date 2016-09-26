using System;
using System.Linq;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.ProjectModel.Graph
{
    public static class LockFileExtensions
    {
        public static readonly int CurrentVersion = 2;

        public static bool IsValidForProject(this LockFile lockFile, Project project)
        {
            string message;
            return lockFile.IsValidForProject(project, out message);
        }

        public static bool IsValidForProject(this LockFile lockFile, Project project, out string message)
        {
            if (lockFile.Version != CurrentVersion)
            {
                message = $"The expected lock file version does not match the actual version";
                return false;
            }

            message = $"Dependencies in {Project.FileName} were modified";

            var actualTargetFrameworks = project.GetTargetFrameworks();

            // The lock file should contain dependencies for each framework plus dependencies shared by all frameworks
            if (lockFile.ProjectFileDependencyGroups.Count != actualTargetFrameworks.Count() + 1)
            {
                Console.WriteLine($"Different count; {lockFile.ProjectFileDependencyGroups.Count} != {actualTargetFrameworks.Count() + 1}");
                return false;
            }

            foreach (var group in lockFile.ProjectFileDependencyGroups)
            {
                IOrderedEnumerable<string> actualDependencies;
                var expectedDependencies = group.Dependencies.OrderBy(x => x);

                // If the framework name is empty, the associated dependencies are shared by all frameworks
                if (string.IsNullOrEmpty(group.FrameworkName))
                {
                    actualDependencies = project.Dependencies
                        .Select(d => d.LibraryRange.ToLockFileDependencyGroupString())
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    var framework = actualTargetFrameworks
                        .FirstOrDefault(f => Equals(f.FrameworkName.DotNetFrameworkName, group.FrameworkName));
                    if (framework == null)
                    {
                        return false;
                    }

                    actualDependencies = framework.Dependencies
                        .Select(d => d.LibraryRange.ToLockFileDependencyGroupString())
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
                }

                if (!actualDependencies.SequenceEqual(expectedDependencies))
                {
                    Console.WriteLine($"ActualDependencies don't match");
                    return false;
                }
            }

            message = null;
            return true;
        }
    }
}
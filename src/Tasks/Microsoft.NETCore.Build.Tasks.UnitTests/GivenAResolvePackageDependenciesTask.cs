using System;
using System.Linq;
using FluentAssertions;
using Xunit;
using System.IO;
using NuGet.ProjectModel;
using Microsoft.Build.Framework;
using System.Collections.Generic;

namespace Microsoft.NETCore.Build.Tasks.UnitTests
{
    public class GivenAResolvePackageDependenciesTask
    {
        [Theory]
        [MemberData("ItemCounts")]
        public void ItRaisesLockFileToMSBuildItems(string projectName, int [] counts)
        {
            var task = GetExecutedTask(projectName);

            task.PackageDefinitions .Count().Should().Be(counts[0]);
            task.FileDefinitions    .Count().Should().Be(counts[1]);
            task.TargetDefinitions  .Count().Should().Be(counts[2]);
            task.PackageDependencies.Count().Should().Be(counts[3]);
            task.FileDependencies   .Count().Should().Be(counts[4]);
        }

        public static IEnumerable<object[]> ItemCounts
        {
            get
            {
                return new[]
                {
                    new object[] {
                        "dotnet.new",
                        new int[] { 110, 2536, 1, 846, 77 }
                    },
                    new object[] {
                        "simple.dependencies",
                        new int[] { 113, 2613, 1, 878, 98}
                    },
                };
            }
        }

        [Theory]
        [InlineData("dotnet.new")]
        [InlineData("simple.dependencies")]
        public void ItAssignsTypeMetaDataToEachDefinition(string projectName)
        {
            var task = GetExecutedTask(projectName);

            Func<ITaskItem[], bool> allTyped =
                (items) => items.All(x => !string.IsNullOrEmpty(x.GetMetadata(MetadataKeys.Type)));

            allTyped(task.PackageDefinitions).Should().BeTrue();
            allTyped(task.FileDefinitions).Should().BeTrue();
            allTyped(task.TargetDefinitions).Should().BeTrue();
        }

        [Theory]
        [InlineData("dotnet.new")]
        [InlineData("simple.dependencies")]
        public void ItsAssignsValidParentTargetsAndPackages(string projectName)
        {
            LockFile lockFile;
            var task = GetExecutedTask(projectName, out lockFile);

            // set of valid targets and packages
            HashSet<string> validTargets = new HashSet<string>(lockFile.Targets.Select(x => x.Name));
            HashSet<string> validPackages = new HashSet<string>(lockFile.Libraries.Select(x => $"{x.Name}/{x.Version.ToString()}"));

            Func<ITaskItem[], bool> allValidParentTarget =
                (items) => items.All(x => validTargets.Contains(x.GetMetadata(MetadataKeys.ParentTarget)));

            Func<ITaskItem[], bool> allValidParentPackage =
                (items) => items.All(
                    x => validPackages.Contains(x.GetMetadata(MetadataKeys.ParentPackage))
                    || string.IsNullOrEmpty(x.GetMetadata(MetadataKeys.ParentPackage)));

            allValidParentTarget(task.PackageDependencies).Should().BeTrue();
            allValidParentTarget(task.FileDependencies).Should().BeTrue();

            allValidParentPackage(task.PackageDependencies).Should().BeTrue();
            allValidParentPackage(task.FileDependencies).Should().BeTrue();
        }

        [Theory]
        [InlineData("dotnet.new")]
        [InlineData("simple.dependencies")]
        public void ItsAssignsValidTopLevelDependencies(string projectName)
        {
            LockFile lockFile;
            var task = GetExecutedTask(projectName, out lockFile);

            var allProjectDeps = lockFile.ProjectFileDependencyGroups
                .SelectMany(group => group.Dependencies);

            var topLevels = task.PackageDependencies
                .Where(t => string.IsNullOrEmpty(t.GetMetadata(MetadataKeys.ParentPackage)))
                .ToList();

            topLevels.Any().Should().BeTrue();

            topLevels
                .Select(t => t.ItemSpec)
                .Select(s => s.Substring(0, s.IndexOf("/")))
                .Should().OnlyContain(p => allProjectDeps.Any(dep => dep.IndexOf(p) != -1));
        }

        private ResolvePackageDependencies GetExecutedTask(string lockFilePrefix)
        {
            LockFile lockFile;
            return GetExecutedTask(lockFilePrefix, out lockFile);
        }

        private ResolvePackageDependencies GetExecutedTask(string lockFilePrefix, out LockFile lockFile)
        {
            lockFile = TestLockFiles.GetLockFile(lockFilePrefix);
            var resolver = new MockPackageResolver();

            var task = new ResolvePackageDependencies(lockFile, resolver)
            {
                ProjectLockFile = lockFile.Path,
                ProjectPath = null
            };

            task.Execute().Should().BeTrue();

            return task;
        }

    }
}
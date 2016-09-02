// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Framework;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Microsoft.NETCore.Build.Tasks.UnitTests.TestLockFiles;

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
        public void ItAssignsValidParentTargetsAndPackages(string projectName)
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
        public void ItAssignsValidTopLevelDependencies(string projectName)
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


        [Theory]
        [InlineData("test.minimal")]
        public void ItAssignsExpectedTopLevelDependencies(string projectName)
        {
            // Libraries:
            // LibA/1.2.3 ==> Top Level Dependency
            // LibB/1.2.3
            // LibC/1.2.3

            LockFile lockFile;
            var task = GetExecutedTask(projectName, out lockFile);

            var topLevels = task.PackageDependencies
                .Where(t => string.IsNullOrEmpty(t.GetMetadata(MetadataKeys.ParentPackage)))
                .ToList();

            topLevels.Count.Should().Be(1);

            var item = topLevels[0];
            item.ItemSpec.Should().Be("LibA/1.2.3");
        }

        [Theory]
        [InlineData("test.minimal")]
        public void ItAssignsPackageDefinitionMetadata(string projectName)
        {
            LockFile lockFile;
            var task = GetExecutedTask(projectName, out lockFile);

            var validPackageNames = new HashSet<string>() {
                "LibA", "LibB", "LibC"
            };

            task.PackageDefinitions.Count().Should().Be(3);

            foreach (var package in task.PackageDefinitions)
            {
                string name = package.GetMetadata(MetadataKeys.Name);
                validPackageNames.Contains(name).Should().BeTrue();
                package.GetMetadata(MetadataKeys.Version).Should().Be("1.2.3");
                package.GetMetadata(MetadataKeys.Type).Should().Be("package");

                // TODO resolved path
                // TODO other package types
            }
        }

        //- Top level projects correspond to expected values
        //- Package definitions have expected metadata(including resolved path)
        //- File definitions have expected metadata(including resolved path)
        //- File definitions excluded placeholders
        //- Analyzer assemblies have expected metadata
        //- Analyzer assemblies produce File dependencies(with matching parent targets)
        //- File definitions get type depending on how they occur in targets(including "unknown")
        //- Target definitions get expected metadata
        //- Expected package dependencies and their metadata
        //- Expected file dependencies and their metadata
        //- Package dependencies with version range

        private ResolvePackageDependencies GetExecutedTask(string lockFilePrefix)
        {
            LockFile lockFile;
            return GetExecutedTask(lockFilePrefix, out lockFile);
        }

        private ResolvePackageDependencies GetExecutedTask(string lockFilePrefix, out LockFile lockFile)
        {
            lockFile = GetLockFile(lockFilePrefix);
            var resolver = new MockPackageResolver();

            var task = new ResolvePackageDependencies(lockFile, resolver)
            {
                ProjectLockFile = lockFile.Path,
                ProjectPath = null,
                ProjectLanguage = null
            };

            task.Execute().Should().BeTrue();

            return task;
        }
    }
}
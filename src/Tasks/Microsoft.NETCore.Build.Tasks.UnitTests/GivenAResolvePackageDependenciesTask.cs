// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Framework;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Microsoft.NETCore.Build.Tasks.UnitTests.LockFileSnippets;

namespace Microsoft.NETCore.Build.Tasks.UnitTests
{
    public class GivenAResolvePackageDependenciesTask
    {
        private const string _packageRoot = "\\root\\packages";

        [Theory]
        [MemberData("ItemCounts")]
        public void ItRaisesLockFileToMSBuildItems(string projectName, int [] counts)
        {
            var task = GetExecutedTaskFromPrefix(projectName);

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
            var task = GetExecutedTaskFromPrefix(projectName);

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
            var task = GetExecutedTaskFromPrefix(projectName, out lockFile);

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
            var task = GetExecutedTaskFromPrefix(projectName, out lockFile);

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
            var task = GetExecutedTaskFromPrefix(projectName, out lockFile);

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
            var task = GetExecutedTaskFromPrefix(projectName, out lockFile);

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
        [Fact]
        public void ItAssignsExpectedTopLevelDependencies2()
        {
            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC),
                    CreateTarget(".NETCoreApp,Version=v1.0/osx.10.11-x64", TargetLibB, TargetLibC),
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn },
                projectFileDependencyGroups: new string[] {
                    CreateProjectFileDependencyGroup("", "LibA >= 1.2.3"), // ==> Top Level Dependency
                    NETCoreGroup, NETCoreOsxGroup
                }
            );

            LockFile lockFile;
            var task = GetExecutedTaskFromContents(lockFileContent, out lockFile);

            var topLevels = task.PackageDependencies
                .Where(t => string.IsNullOrEmpty(t.GetMetadata(MetadataKeys.ParentPackage)));

            topLevels.Count().Should().Be(1);

            topLevels.First().ItemSpec.Should().Be("LibA/1.2.3");
        }

        //- Target definitions get expected metadata
        [Fact]
        public void ItAssignsTargetDefinitionMetadata()
        {
            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC),
                    CreateTarget(".NETCoreApp,Version=v1.0/osx.10.11-x64", TargetLibB, TargetLibC),
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn },
                projectFileDependencyGroups: new string[] { ProjectGroup, NETCoreGroup, NETCoreOsxGroup }
            );

            LockFile lockFile;
            var task = GetExecutedTaskFromContents(lockFileContent, out lockFile);

            task.TargetDefinitions.Count().Should().Be(2);

            var target = task.TargetDefinitions.Where(t => t.ItemSpec == ".NETCoreApp,Version=v1.0").First();
            target.GetMetadata(MetadataKeys.RuntimeIdentifier).Should().BeEmpty();
            target.GetMetadata(MetadataKeys.TargetFrameworkMoniker).Should().Be(".NETCoreApp,Version=v1.0");
            target.GetMetadata(MetadataKeys.FrameworkName).Should().Be(".NETCoreApp");
            target.GetMetadata(MetadataKeys.FrameworkVersion).Should().Be("1.0.0.0");
            target.GetMetadata(MetadataKeys.Type).Should().Be("target");

            target = task.TargetDefinitions.Where(t => t.ItemSpec == ".NETCoreApp,Version=v1.0/osx.10.11-x64").First();
            target.GetMetadata(MetadataKeys.RuntimeIdentifier).Should().Be("osx.10.11-x64");
            target.GetMetadata(MetadataKeys.TargetFrameworkMoniker).Should().Be(".NETCoreApp,Version=v1.0");
            target.GetMetadata(MetadataKeys.FrameworkName).Should().Be(".NETCoreApp");
            target.GetMetadata(MetadataKeys.FrameworkVersion).Should().Be("1.0.0.0");
            target.GetMetadata(MetadataKeys.Type).Should().Be("target");
        }

        //- Package definitions have expected metadata(including resolved path)
        [Fact]
        public void ItAssignsPackageDefinitionMetadata2()
        {
            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC),
                    CreateTarget(".NETCoreApp,Version=v1.0/osx.10.11-x64", TargetLibB, TargetLibC),
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn },
                projectFileDependencyGroups: new string[] { ProjectGroup, NETCoreGroup, NETCoreOsxGroup }
            );

            LockFile lockFile;
            var task = GetExecutedTaskFromContents(lockFileContent, out lockFile);

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
                package.GetMetadata(MetadataKeys.Path).Should().BeEmpty(); // value is null for type 'package'
                package.GetMetadata(MetadataKeys.ResolvedPath).Should().Be($"{_packageRoot}\\{name}\\1.2.3\\path");

                // TODO other package types
            }
        }

        //- Expected package dependencies and their metadata
        [Fact]
        public void ItAssignsPackageDependenciesMetadata()
        {
            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC)
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn },
                projectFileDependencyGroups: new string[] { ProjectGroup, NETCoreGroup }
            );

            LockFile lockFile;
            var task = GetExecutedTaskFromContents(lockFileContent, out lockFile);

            task.PackageDependencies.Count().Should().Be(3);

            var packageDep = task.PackageDependencies.Where(t => t.ItemSpec == "LibA/1.2.3").First();
            packageDep.GetMetadata(MetadataKeys.ParentPackage).Should().BeEmpty();
            packageDep.GetMetadata(MetadataKeys.ParentTarget).Should().Be(".NETCoreApp,Version=v1.0");

            packageDep = task.PackageDependencies.Where(t => t.ItemSpec == "LibB/1.2.3").First();
            packageDep.GetMetadata(MetadataKeys.ParentPackage).Should().Be("LibA/1.2.3");
            packageDep.GetMetadata(MetadataKeys.ParentTarget).Should().Be(".NETCoreApp,Version=v1.0");

            packageDep = task.PackageDependencies.Where(t => t.ItemSpec == "LibC/1.2.3").First();
            packageDep.GetMetadata(MetadataKeys.ParentPackage).Should().Be("LibB/1.2.3");
            packageDep.GetMetadata(MetadataKeys.ParentTarget).Should().Be(".NETCoreApp,Version=v1.0");
        }

        //- File definitions have expected metadata(including resolved path)
        //- File definitions get type depending on how they occur in targets(including "unknown")
        //- Expected file dependencies and their metadata
        [Fact]
        public void ItAssignsFileDefinitionMetadata()
        {
            string targetLibB = CreateTargetLibrary("LibB/1.2.3", "package",
                frameworkAssemblies: new string[] { "System.Some.Lib" },
                dependencies: new string[] { "\"LibC\": \"1.2.3\"" },
                compile: new string[] { CreateFileItem("lib/file/C1.dll") },
                runtime: new string[] { CreateFileItem("lib/file/R1.dll") }
                );

            string libBDefn = CreateLibrary("LibB/1.2.3", "package",
                "lib/file/C1.dll", "lib/file/R1.dll");

            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, targetLibB, TargetLibC),
                    CreateTarget(".NETCoreApp,Version=v1.0/osx.10.11-x64", targetLibB, TargetLibC),
                },
                libraries: new string[] {
                    LibADefn, libBDefn, LibCDefn
                },
                projectFileDependencyGroups: new string[] { ProjectGroup, NETCoreGroup, NETCoreOsxGroup }
            );

            LockFile lockFile;
            var task = GetExecutedTaskFromContents(lockFileContent, out lockFile);

            IEnumerable<ITaskItem> fileDefns, fileDeps;

            // compile
            fileDefns = task.FileDefinitions
                .Where(t => t.ItemSpec == "LibB/1.2.3/lib/file/C1.dll");
            fileDefns.Count().Should().Be(1);
            fileDefns.First().GetMetadata(MetadataKeys.Path).Should().Be("lib/file/C1.dll");
            fileDefns.First().GetMetadata(MetadataKeys.ResolvedPath).Should().Be($"{_packageRoot}\\LibB\\1.2.3\\path\\lib\\file\\C1.dll");
            fileDefns.First().GetMetadata(MetadataKeys.Type).Should().Be("assembly");

            fileDeps = task.FileDependencies
                .Where(t => t.ItemSpec == "LibB/1.2.3/lib/file/C1.dll");
            fileDeps.Count().Should().Be(2);
            fileDeps.First().GetMetadata(MetadataKeys.FileGroup).Should().Be(FileGroup.CompileTimeAssembly.ToString());
            fileDeps.First().GetMetadata(MetadataKeys.ParentTarget).Should().Be(".NETCoreApp,Version=v1.0");
            fileDeps.First().GetMetadata(MetadataKeys.ParentPackage).Should().Be("LibB/1.2.3");
        }

        //- File definitions excluded placeholders
        [Fact]
        public void ItExcludesPlaceholderFiles()
        {
            string targetLibC = CreateTargetLibrary("LibC/1.2.3", "package",
                compile: new string[] {
                    CreateFileItem("lib/file/G.dll"), CreateFileItem("lib/file/H.dll"),
                    CreateFileItem("ref/netstandard1.3/_._")
                });

            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, targetLibC),
                    CreateTarget(".NETCoreApp,Version=v1.0/osx.10.11-x64", TargetLibB, targetLibC),
                },
                libraries: new string[] {
                    LibADefn, LibBDefn, LibCDefn,
                    CreateLibrary("LibX/1.2.3", "package", "lib/file/Z.dll", "lib/file/_._")
                },
                projectFileDependencyGroups: new string[] { ProjectGroup, NETCoreGroup, NETCoreOsxGroup }
            );

            LockFile lockFile;
            var task = GetExecutedTaskFromContents(lockFileContent, out lockFile);

            task.FileDefinitions
                .Any(t => t.GetMetadata(MetadataKeys.Path) == "lib/file/Z.dll")
                .Should().BeTrue();

            task.FileDefinitions
                .Any(t => t.GetMetadata(MetadataKeys.Path) == "lib/file/_._")
                .Should().BeFalse();

            task.FileDependencies
                .Any(t => t.ItemSpec == "LibC/1.2.3/lib/file/G.dll")
                .Should().BeTrue();

            task.FileDependencies
                .Any(t => t.ItemSpec == "LibC/1.2.3/lib/file/_._")
                .Should().BeFalse();
        }

        //- Analyzer assemblies have expected metadata
        //- Analyzer assemblies produce File dependencies(with matching parent targets)
        [Fact]
        public void ItAssignsAnalyzerMetadata()
        {
            string libCDefn = CreateLibrary("LibC/1.2.3", "package", 
                "lib/file/G.dll", "lib/file/H.dll", "lib/file/I.dll",
                "analyzers/dotnet/cs/Microsoft.CodeAnalysis.Analyzers.dll",
                "analyzers/dotnet/cs/Microsoft.CodeAnalysis.CSharp.Analyzers.dll",
                "analyzers/dotnet/vb/Microsoft.CodeAnalysis.Analyzers.dll",
                "analyzers/dotnet/vb/Microsoft.CodeAnalysis.VisualBasic.Analyzers.dll",
                "analyzers/dotnet/cs/Microsoft.CodeAnalysis.CSharp.Analyzers.txt", // not analyzer
                "lib/file/Microsoft.CodeAnalysis.VisualBasic.Analyzers.dll" // not analyzer
                );

            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC),
                    CreateTarget(".NETCoreApp,Version=v1.0/osx.10.11-x64", TargetLibB, TargetLibC),
                },
                libraries: new string[] {
                    LibADefn, LibBDefn, libCDefn
                },
                projectFileDependencyGroups: new string[] { ProjectGroup, NETCoreGroup, NETCoreOsxGroup }
            );

            LockFile lockFile;
            var task = GetExecutedTaskFromContents(lockFileContent, out lockFile);

            IEnumerable<ITaskItem> fileDefns;

            fileDefns = task.FileDefinitions
                .Where(t => t.GetMetadata(MetadataKeys.Type) == "AnalyzerAssembly");
            fileDefns.Count().Should().Be(4);

            var analyzers = new string[] {
                "analyzers/dotnet/cs/Microsoft.CodeAnalysis.Analyzers.dll",
                "analyzers/dotnet/cs/Microsoft.CodeAnalysis.CSharp.Analyzers.dll",
                "analyzers/dotnet/vb/Microsoft.CodeAnalysis.Analyzers.dll",
                "analyzers/dotnet/vb/Microsoft.CodeAnalysis.VisualBasic.Analyzers.dll",
            };
            var expectedTargets = new string[] {
                ".NETCoreApp,Version=v1.0",
                ".NETCoreApp,Version=v1.0/osx.10.11-x64"
            };

            foreach (var analyzer in analyzers)
            {
                var fileKey = $"LibC/1.2.3/{analyzer}";
                var item = task.FileDefinitions.Where(t => t.ItemSpec == fileKey).First();
                item.GetMetadata(MetadataKeys.Type).Should().Be("AnalyzerAssembly");
                item.GetMetadata(MetadataKeys.Path).Should().Be(analyzer);

                // expect two file dependencies for each
                var fileDeps = task.FileDependencies.Where(t => t.ItemSpec == fileKey);
                fileDeps.Count().Should().Be(2);

                var parentTargets = fileDeps.Select(f => f.GetMetadata(MetadataKeys.ParentTarget));
                parentTargets.Should().Contain(expectedTargets);

                fileDeps.All(f => f.GetMetadata(MetadataKeys.ParentPackage) == "LibC/1.2.3");
            }
        }

        //- Top Levels can come from default or other tfms
        //- Package dependencies with version range
        [Fact]
        public void ItUsesMinVersionFromPackageDependencyRanges()
        {
            string targetLibC = CreateTargetLibrary("LibC/1.2.3", "package",
                dependencies: new string[] {
                    "\"Dep.Lib.Alpha\": \"4.0.0\"",
                    "\"Dep.Lib.Beta\": \"[4.0.0]\"",
                    "\"Dep.Lib.Chi\": \"[4.0.0, 5.0.0)\"",
                    "\"Dep.Lib.Delta\": \"[4.0.0)\"",
                });

            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, targetLibC),
                },
                libraries: new string[] {
                    LibADefn, LibBDefn, LibCDefn,
                    CreateLibrary("Dep.Lib.Alpha/4.0.0", "package", "lib/file/Alpha.dll"),
                    CreateLibrary("Dep.Lib.Beta/4.0.0",  "package", "lib/file/Beta.dll"),
                    CreateLibrary("Dep.Lib.Chi/4.0.0",   "package", "lib/file/Chi.dll"),
                    CreateLibrary("Dep.Lib.Delta/4.0.0", "package", "lib/file/Delta.dll"),
                },
                projectFileDependencyGroups: new string[] { ProjectGroup, NETCoreGroup, NETCoreOsxGroup }
            );

            LockFile lockFile;
            var task = GetExecutedTaskFromContents(lockFileContent, out lockFile);

            task.PackageDependencies
                .Where(t => t.ItemSpec.StartsWith("Dep.Lib."))
                .Count().Should().Be(4);

            task.PackageDependencies
                .Any(t => t.ItemSpec == "Dep.Lib.Alpha/4.0.0")
                .Should().BeTrue();

            task.PackageDependencies
                .Any(t => t.ItemSpec == "Dep.Lib.Beta/4.0.0")
                .Should().BeTrue();

            task.PackageDependencies
                .Any(t => t.ItemSpec == "Dep.Lib.Chi/4.0.0")
                .Should().BeTrue();

            task.PackageDependencies
                .Any(t => t.ItemSpec == "Dep.Lib.Delta/4.0.0")
                .Should().BeTrue();
        }

        private ResolvePackageDependencies GetExecutedTaskFromPrefix(string lockFilePrefix)
        {
            LockFile lockFile;
            return GetExecutedTaskFromPrefix(lockFilePrefix, out lockFile);
        }

        private ResolvePackageDependencies GetExecutedTaskFromPrefix(string lockFilePrefix, out LockFile lockFile)
        {
            lockFile = TestLockFiles.GetLockFile(lockFilePrefix);
            return GetExecutedTask(lockFile);
        }

        private ResolvePackageDependencies GetExecutedTaskFromContents(string lockFileContents)
        {
            LockFile lockFile;
            return GetExecutedTaskFromContents(lockFileContents, out lockFile);
        }

        private ResolvePackageDependencies GetExecutedTaskFromContents(string lockFileContents, out LockFile lockFile)
        {
            lockFile = TestLockFiles.CreateLockFile(lockFileContents);
            return GetExecutedTask(lockFile);
        }

        private ResolvePackageDependencies GetExecutedTask(LockFile lockFile)
        {
            var resolver = new MockPackageResolver(_packageRoot);

            var task = new ResolvePackageDependencies(lockFile, resolver)
            {
                ProjectLockFile = lockFile.Path,
                ProjectPath = null,
                ProjectLanguage = null
            };

            task.Execute().Should().BeTrue();

            return task;
        }

        private static readonly string ProjectGroup =
            CreateProjectFileDependencyGroup("", "LibA >= 1.2.3");

        private static readonly string NETCoreGroup =
            CreateProjectFileDependencyGroup(".NETCoreApp,Version=v1.0");

        private static readonly string NETCoreOsxGroup = 
            CreateProjectFileDependencyGroup(".NETCoreApp,Version=v1.0/osx.10.11-x64");

        private static readonly string LibADefn = 
            CreateLibrary("LibA/1.2.3", "package", "lib/file/A.dll", "lib/file/B.dll", "lib/file/C.dll");

        private static readonly string LibBDefn =
            CreateLibrary("LibB/1.2.3", "package", "lib/file/D.dll", "lib/file/E.dll", "lib/file/F.dll");

        private static readonly string LibCDefn =
            CreateLibrary("LibC/1.2.3", "package", "lib/file/G.dll", "lib/file/H.dll", "lib/file/I.dll");

        private static readonly string TargetLibA = CreateTargetLibrary("LibA/1.2.3", "package",
            dependencies: new string[] { "\"LibB\": \"1.2.3\"" },
            frameworkAssemblies: new string[] { "System.Some.Lib" },
            compile: new string[] { CreateFileItem("lib/file/A.dll"), CreateFileItem("lib/file/B.dll") },
            runtime: new string[] { CreateFileItem("lib/file/A.dll"), CreateFileItem("lib/file/B.dll") }
            );

        private static readonly string TargetLibB = CreateTargetLibrary("LibB/1.2.3", "package",
            dependencies: new string[] { "\"LibC\": \"1.2.3\"" },
            frameworkAssemblies: new string[] { "System.Some.Lib" },
            compile: new string[] { CreateFileItem("lib/file/D.dll"), CreateFileItem("lib/file/E.dll") },
            runtime: new string[] { CreateFileItem("lib/file/D.dll"), CreateFileItem("lib/file/E.dll") }
            );

        private static readonly string TargetLibC = CreateTargetLibrary("LibC/1.2.3", "package",
            compile: new string[] { CreateFileItem("lib/file/G.dll"), CreateFileItem("lib/file/H.dll") },
            runtime: new string[] { CreateFileItem("lib/file/G.dll"), CreateFileItem("lib/file/H.dll") }
            );
    }
}
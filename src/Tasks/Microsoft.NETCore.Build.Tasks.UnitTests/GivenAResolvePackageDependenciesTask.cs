// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Framework;
using NuGet.ProjectModel;
using System;
using System.Collections.Generic;
using System.IO;
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

        [Fact]
        public void ItAssignsExpectedTopLevelDependencies()
        {
            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC),
                    CreateTarget(".NETCoreApp,Version=v1.0/osx.10.11-x64", TargetLibA, TargetLibB, TargetLibC),
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn },
                projectFileDependencyGroups: new string[] {
                    CreateProjectFileDependencyGroup("", "LibA >= 1.2.3"), // ==> Top Level Dependency
                    NETCoreGroup, NETCoreOsxGroup
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent);

            var topLevels = task.PackageDependencies
                .Where(t => string.IsNullOrEmpty(t.GetMetadata(MetadataKeys.ParentPackage)));

            topLevels.Count().Should().Be(2);
            topLevels.All(t => t.ItemSpec == "LibA/1.2.3").Should().BeTrue();
            topLevels.Select(t => t.GetMetadata(MetadataKeys.ParentTarget))
                .Should().Contain(new string[] {
                    ".NETCoreApp,Version=v1.0", ".NETCoreApp,Version=v1.0/osx.10.11-x64"
                });
        }

        [Fact]
        public void ItAssignsExpectedTopLevelDependenciesFromAllTargets()
        {
            string targetLibD = CreateTargetLibrary("LibD/1.2.3", "package",
                dependencies: new string[] {
                    "\"LibC\": \"1.2.3\""
                });

            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC, targetLibD),
                    CreateTarget(".NETCoreApp,Version=v1.0/osx.10.11-x64", TargetLibA, TargetLibB, TargetLibC),
                },
                libraries: new string[] {
                    LibADefn, LibBDefn, LibCDefn,
                    CreateLibrary("LibD/1.2.3", "package", "lib/file/Z.dll")
                },
                projectFileDependencyGroups: new string[] {
                    CreateProjectFileDependencyGroup("", "LibA >= 1.2.3"), // Default
                    CreateProjectFileDependencyGroup(".NETCoreApp,Version=v1.0", "LibD >= 1.2.3"), // NETCore only
                    NETCoreOsxGroup
                }
            );

            var task = GetExecutedTaskFromContents(lockFileContent);

            var topLevels = task.PackageDependencies
                .Where(t => string.IsNullOrEmpty(t.GetMetadata(MetadataKeys.ParentPackage)));

            topLevels.Count().Should().Be(3);
            topLevels.Where(t => t.ItemSpec == "LibA/1.2.3").Count().Should().Be(2);
            topLevels.Where(t => t.ItemSpec == "LibA/1.2.3")
                .Select(t => t.GetMetadata(MetadataKeys.ParentTarget))
                .Should().Contain(new string[] {
                    ".NETCoreApp,Version=v1.0", ".NETCoreApp,Version=v1.0/osx.10.11-x64"
                });

            topLevels.Where(t => t.ItemSpec == "LibD/1.2.3").Count().Should().Be(1);
            topLevels.Where(t => t.ItemSpec == "LibD/1.2.3")
                .Select(t => t.GetMetadata(MetadataKeys.ParentTarget))
                .Should().Contain(new string[] {
                    ".NETCoreApp,Version=v1.0"
                });
        }

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

            var task = GetExecutedTaskFromContents(lockFileContent);

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

        [Fact]
        public void ItAssignsPackageDefinitionMetadata()
        {
            // project lib
            string classLibPDefn = CreateProjectLibrary("ClassLibP/1.2.3", 
                path: "../ClassLibP/project.json", 
                msbuildProject: "../ClassLibP/ClassLibP.csproj");

            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC),
                    CreateTarget(".NETCoreApp,Version=v1.0/osx.10.11-x64", TargetLibB, TargetLibC),
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn, classLibPDefn },
                projectFileDependencyGroups: new string[] { ProjectGroup, NETCoreGroup, NETCoreOsxGroup }
            );

            LockFile lockFile;
            var task = GetExecutedTaskFromContents(lockFileContent, out lockFile);

            var validPackageNames = new HashSet<string>() {
                "LibA", "LibB", "LibC", "ClassLibP"
            };

            task.PackageDefinitions.Count().Should().Be(4);

            foreach (var package in task.PackageDefinitions)
            {
                string name = package.GetMetadata(MetadataKeys.Name);
                validPackageNames.Contains(name).Should().BeTrue();
                package.GetMetadata(MetadataKeys.Version).Should().Be("1.2.3");

                if (name == "ClassLibP")
                {
                    package.GetMetadata(MetadataKeys.Type).Should().Be("project");
                    package.GetMetadata(MetadataKeys.Path).Should().Be($"../ClassLibP/project.json");

                    var resolvedPath = Path.GetDirectoryName(Path.GetFullPath(lockFile.Path));
                    resolvedPath = Path.GetFullPath(Path.Combine(resolvedPath, "../ClassLibP/ClassLibP.csproj"));
                    package.GetMetadata(MetadataKeys.ResolvedPath).Should().Be(resolvedPath);
                }
                else
                {
                    package.GetMetadata(MetadataKeys.Type).Should().Be("package");
                    package.GetMetadata(MetadataKeys.Path).Should().Be($"{name}/1.2.3");
                    package.GetMetadata(MetadataKeys.ResolvedPath).Should().Be($"{_packageRoot}\\{name}\\1.2.3\\path");
                }
            }
        }

        [Fact]
        public void ItAssignsPackageDependenciesMetadata()
        {
            // project lib
            string classLibPDefn = CreateProjectLibrary("ClassLibP/1.2.3",
                path: "../ClassLibP/project.json",
                msbuildProject: "../ClassLibP/ClassLibP.csproj");

            string targetLibP = CreateTargetLibrary("ClassLibP/1.2.3", "project",
                dependencies: new string[] { "\"LibC\": \"1.2.3\"" }
                );

            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibB, TargetLibC, targetLibP)
                },
                libraries: new string[] { LibADefn, LibBDefn, LibCDefn, classLibPDefn },
                projectFileDependencyGroups: new string[] { ProjectGroup, NETCoreGroup }
            );

            LockFile lockFile;
            var task = GetExecutedTaskFromContents(lockFileContent, out lockFile);

            task.PackageDependencies.Count().Should().Be(4);

            var packageDep = task.PackageDependencies.Where(t => t.ItemSpec == "LibA/1.2.3").First();
            packageDep.GetMetadata(MetadataKeys.ParentPackage).Should().BeEmpty();
            packageDep.GetMetadata(MetadataKeys.ParentTarget).Should().Be(".NETCoreApp,Version=v1.0");

            packageDep = task.PackageDependencies.Where(t => t.ItemSpec == "LibB/1.2.3").First();
            packageDep.GetMetadata(MetadataKeys.ParentPackage).Should().Be("LibA/1.2.3");
            packageDep.GetMetadata(MetadataKeys.ParentTarget).Should().Be(".NETCoreApp,Version=v1.0");

            // LibC has both a package and project that depend on it
            var packageDeps = task.PackageDependencies.Where(t => t.ItemSpec == "LibC/1.2.3");
            packageDeps.Count().Should().Be(2);
            packageDeps.Select(t => t.GetMetadata(MetadataKeys.ParentPackage))
                .Should().Contain(new string[] { "LibB/1.2.3", "ClassLibP/1.2.3" });
            packageDeps.Select(t => t.GetMetadata(MetadataKeys.ParentTarget))
                .Should().OnlyContain(s => s == ".NETCoreApp,Version=v1.0");
        }

        [Fact]
        public void ItAssignsFileDefinitionMetadata()
        {
            var expectedTypes = new Dictionary<string, string>()
            {
                { "lib/file/U1.dll",                "unknown" },
                { "lib/file/C1.dll",                "assembly" }, // compile
                { "lib/file/R1.dll",                "assembly" }, // runtime
                { "lib/file/N1.dll",                "assembly" }, // native
                { "lib/file/R2.resources.dll",      "assembly" }, // resource
                { "runtimes/osx/native/R3.dylib",   "assembly" }, // runtime target
                { "System.Some.Lib",                "frameworkAssembly" },
                { "contentFiles/any/images/C2.png", "content" }, 
            };

            string libBAllAssetsDefn = CreateLibrary("LibB/1.2.3", "package", expectedTypes.Keys.ToArray());

            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibBAllAssets, TargetLibC),
                    CreateTarget(".NETCoreApp,Version=v1.0/osx.10.11-x64", TargetLibBAllAssets, TargetLibC),
                },
                libraries: new string[] {
                    LibADefn, libBAllAssetsDefn, LibCDefn
                },
                projectFileDependencyGroups: new string[] { ProjectGroup, NETCoreGroup, NETCoreOsxGroup }
            );

            var task = GetExecutedTaskFromContents(lockFileContent);

            IEnumerable<ITaskItem> fileDefns;

            foreach (var pair in expectedTypes)
            {
                fileDefns = task.FileDefinitions
                    .Where(t => t.ItemSpec == $"LibB/1.2.3/{pair.Key}");
                fileDefns.Count().Should().Be(1);
                fileDefns.First().GetMetadata(MetadataKeys.Type).Should().Be(pair.Value);
                fileDefns.First().GetMetadata(MetadataKeys.Path).Should().Be(pair.Key);
                fileDefns.First().GetMetadata(MetadataKeys.ResolvedPath)
                    .Should().Be($"{_packageRoot}\\LibB\\1.2.3\\path\\{pair.Key.Replace("/", "\\")}");
            }
        }

        [Fact]
        public void ItAssignsFileDependenciesMetadata()
        {
            var expectedFileGroups = new Dictionary<string, string>()
            {
                { "lib/file/U1.dll",                null },
                { "lib/file/C1.dll",                FileGroup.CompileTimeAssembly.ToString() },
                { "lib/file/R1.dll",                FileGroup.RuntimeAssembly.ToString() }, 
                { "lib/file/N1.dll",                FileGroup.NativeLibrary.ToString() }, 
                { "lib/file/R2.resources.dll",      FileGroup.ResourceAssembly.ToString() }, 
                { "runtimes/osx/native/R3.dylib",   FileGroup.RuntimeTarget.ToString() }, 
                { "System.Some.Lib",                FileGroup.FrameworkAssembly.ToString() },
                { "contentFiles/any/images/C2.png", FileGroup.ContentFile.ToString() },
            };

            string libBAllAssetsDefn = CreateLibrary("LibB/1.2.3", "package", expectedFileGroups.Keys.ToArray());

            string lockFileContent = CreateLockFileSnippet(
                targets: new string[] {
                    CreateTarget(".NETCoreApp,Version=v1.0", TargetLibA, TargetLibBAllAssets, TargetLibC),
                    CreateTarget(".NETCoreApp,Version=v1.0/osx.10.11-x64", TargetLibBAllAssets, TargetLibC),
                },
                libraries: new string[] {
                    LibADefn, libBAllAssetsDefn, LibCDefn
                },
                projectFileDependencyGroups: new string[] { ProjectGroup, NETCoreGroup, NETCoreOsxGroup }
            );

            var task = GetExecutedTaskFromContents(lockFileContent);

            IEnumerable<ITaskItem> fileDeps;

            foreach (var pair in expectedFileGroups)
            {
                if (pair.Value == null)
                {
                    // these files do not appear as a dependency anywhere
                    task.FileDependencies
                        .Where(t => t.ItemSpec == $"LibB/1.2.3/{pair.Key}")
                        .Should().BeEmpty();
                }
                else
                {
                    fileDeps = task.FileDependencies
                        .Where(t => t.ItemSpec == $"LibB/1.2.3/{pair.Key}");
                    fileDeps.Count().Should().Be(2);
                    fileDeps.Select(t => t.GetMetadata(MetadataKeys.FileGroup))
                        .Should().OnlyContain(s => s == pair.Value);
                    fileDeps.Select(t => t.GetMetadata(MetadataKeys.ParentPackage))
                        .Should().OnlyContain(s => s == "LibB/1.2.3");
                    fileDeps.Select(t => t.GetMetadata(MetadataKeys.ParentTarget))
                        .Should().Contain(new string[] { ".NETCoreApp,Version=v1.0", ".NETCoreApp,Version=v1.0/osx.10.11-x64" });
                }
            }
        }

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

            var task = GetExecutedTaskFromContents(lockFileContent);

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

        [Fact]
        public void ItAddsAnalyzerMetadataAndFileDependencies()
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

            var task = GetExecutedTaskFromContents(lockFileContent);

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

                // expect two file dependencies for each, one per target
                var fileDeps = task.FileDependencies.Where(t => t.ItemSpec == fileKey);

                fileDeps.Count().Should().Be(2);

                fileDeps.Select(f => f.GetMetadata(MetadataKeys.ParentTarget))
                    .Should().Contain(expectedTargets);

                fileDeps.All(f => f.GetMetadata(MetadataKeys.ParentPackage) == "LibC/1.2.3");
            }
        }

        [Fact]
        public void ItFiltersAnalyzersByProjectLanguage()
        {
            string projectLanguage = "cs";

            // expected included analyzers
            string[] expectIncluded = new string[] {
                "analyzers/dotnet/IncludedAlpha.dll",
                "analyzers/dotnet/cs/IncludedBeta.dll",
                "analyzers/dotnet/cs/vb/IncludedChi.dll",
            };

            // expected excluded files
            string[] expectExcluded = new string[] {
                "analyzers/dotnet/vb/ExcludedAlpha.dll",
                "analyzers/dotnet/ExcludedBeta.txt",
                "analyzers/dotnet/cs/ExcludedChi.txt",
                "dotnet/ExcludedDelta.dll",
                "dotnet/cs/ExcludedEpsilon.dll"
            };

            var libCFiles = new List<string>()
            {
                "lib/file/G.dll", "lib/file/H.dll", "lib/file/I.dll"
            };
            libCFiles.AddRange(expectIncluded);
            libCFiles.AddRange(expectExcluded);

            string libCDefn = CreateLibrary("LibC/1.2.3", "package", libCFiles.ToArray());

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

            LockFile lockFile = TestLockFiles.CreateLockFile(lockFileContent);
            var task = new ResolvePackageDependencies(lockFile, new MockPackageResolver())
            {
                ProjectLockFile = lockFile.Path,
                ProjectPath = null,
                ProjectLanguage = projectLanguage // set language
            };
            task.Execute().Should().BeTrue();

            IEnumerable<ITaskItem> fileDefns;

            fileDefns = task.FileDefinitions
                .Where(t => t.GetMetadata(MetadataKeys.Type) == "AnalyzerAssembly");
            fileDefns.Count().Should().Be(3);

            var expectedTargets = new string[] {
                ".NETCoreApp,Version=v1.0",
                ".NETCoreApp,Version=v1.0/osx.10.11-x64"
            };

            foreach (var analyzer in expectIncluded)
            {
                var fileKey = $"LibC/1.2.3/{analyzer}";
                var item = task.FileDefinitions.Where(t => t.ItemSpec == fileKey).First();
                item.GetMetadata(MetadataKeys.Type).Should().Be("AnalyzerAssembly");
                item.GetMetadata(MetadataKeys.Path).Should().Be(analyzer);

                // expect two file dependencies for each, one per target
                var fileDeps = task.FileDependencies.Where(t => t.ItemSpec == fileKey);

                fileDeps.Count().Should().Be(2);

                fileDeps.Select(f => f.GetMetadata(MetadataKeys.ParentTarget))
                    .Should().Contain(expectedTargets);

                fileDeps.All(f => f.GetMetadata(MetadataKeys.ParentPackage) == "LibC/1.2.3");
            }

            foreach (var otherFile in expectExcluded)
            {
                var fileKey = $"LibC/1.2.3/{otherFile}";
                var item = task.FileDefinitions.Where(t => t.ItemSpec == fileKey).First();
                item.GetMetadata(MetadataKeys.Type).Should().NotBe("AnalyzerAssembly");

                // expect no file dependencies for each
                task.FileDependencies.Where(t => t.ItemSpec == fileKey)
                    .Should().BeEmpty();
            }
        }

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

            var task = GetExecutedTaskFromContents(lockFileContent);

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
    }
}
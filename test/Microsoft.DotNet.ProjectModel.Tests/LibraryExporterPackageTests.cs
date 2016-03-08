// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.DotNet.ProjectModel.Graph;
using Microsoft.DotNet.ProjectModel.Resolution;
using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class LibraryExporterPackageTests
    {
        private const string PackagePath = "PackagePath";

        private PackageDescription CreateDescription(LockFileTargetLibrary target = null, LockFilePackageLibrary package = null)
        {
            return new PackageDescription(PackagePath,
                package ?? new LockFilePackageLibrary(),
                target ?? new LockFileTargetLibrary(),
                new List<LibraryRange>(), compatible: true, resolved: true);
        }

        [Fact]
        public void ExportsPackageNativeLibraries()
        {
            var description = CreateDescription(
                new LockFileTargetLibrary()
                {
                    NativeLibraries = new List<LockFileItem>()
                    {
                        { new LockFileItem() { Path = "lib/Native.so" } }
                    }
                });

            var result = ExportSingle(description);
            result.NativeLibraries.Should().HaveCount(1);

            var libraryAsset = result.NativeLibraries.First();
            libraryAsset.Name.Should().Be("Native");
            libraryAsset.Transform.Should().BeNull();
            libraryAsset.RelativePath.Should().Be("lib/Native.so");
            libraryAsset.ResolvedPath.Should().Be(Path.Combine(PackagePath, "lib/Native.so"));
        }

        [Fact]
        public void ExportsPackageCompilationAssebmlies()
        {
            var description = CreateDescription(
                new LockFileTargetLibrary()
                {
                    CompileTimeAssemblies = new List<LockFileItem>()
                    {
                        { new LockFileItem() { Path = "ref/Native.dll" } }
                    }
                });

            var result = ExportSingle(description);
            result.CompilationAssemblies.Should().HaveCount(1);

            var libraryAsset = result.CompilationAssemblies.First();
            libraryAsset.Name.Should().Be("Native");
            libraryAsset.Transform.Should().BeNull();
            libraryAsset.RelativePath.Should().Be("ref/Native.dll");
            libraryAsset.ResolvedPath.Should().Be(Path.Combine(PackagePath, "ref/Native.dll"));
        }

        [Fact]
        public void ExportsPackageRuntimeAssebmlies()
        {
            var description = CreateDescription(
                new LockFileTargetLibrary()
                {
                    RuntimeAssemblies = new List<LockFileItem>()
                    {
                        { new LockFileItem() { Path = "ref/Native.dll" } }
                    }
                });

            var result = ExportSingle(description);
            result.RuntimeAssemblies.Should().HaveCount(1);

            var libraryAsset = result.RuntimeAssemblies.First();
            libraryAsset.Name.Should().Be("Native");
            libraryAsset.Transform.Should().BeNull();
            libraryAsset.RelativePath.Should().Be("ref/Native.dll");
            libraryAsset.ResolvedPath.Should().Be(Path.Combine(PackagePath, "ref/Native.dll"));
        }

        [Fact]
        public void ExportsSources()
        {
            var description = CreateDescription(
               package: new LockFilePackageLibrary()
               {
                   Files = new List<string>()
                   {
                      Path.Combine("shared", "file.cs")
                   }
               });

            var result = ExportSingle(description);
            result.SourceReferences.Should().HaveCount(1);

            var libraryAsset = result.SourceReferences.First();
            libraryAsset.Name.Should().Be("file");
            libraryAsset.Transform.Should().BeNull();
            libraryAsset.RelativePath.Should().Be(Path.Combine("shared", "file.cs"));
            libraryAsset.ResolvedPath.Should().Be(Path.Combine(PackagePath, "shared", "file.cs"));
        }

        [Fact]
        public void ExportsCopyToOutputContentFiles()
        {
            var description = CreateDescription(
                new LockFileTargetLibrary()
                {
                    ContentFiles = new List<LockFileContentFile>()
                    {
                        new LockFileContentFile()
                        {
                            CopyToOutput = true,
                            Path = Path.Combine("content", "file.txt"),
                            OutputPath = Path.Combine("Out","Path.txt"),
                            PPOutputPath = "something"
                        }
                    }
                });

            var result = ExportSingle(description);
            result.RuntimeAssets.Should().HaveCount(1);

            var libraryAsset = result.RuntimeAssets.First();
            libraryAsset.Transform.Should().NotBeNull();
            libraryAsset.RelativePath.Should().Be(Path.Combine("Out", "Path.txt"));
            libraryAsset.ResolvedPath.Should().Be(Path.Combine(PackagePath, "content", "file.txt"));
        }


        [Fact]
        public void ExportsResourceContentFiles()
        {
            var description = CreateDescription(
                new LockFileTargetLibrary()
                {
                    ContentFiles = new List<LockFileContentFile>()
                    {
                        new LockFileContentFile()
                        {
                            BuildAction = BuildAction.EmbeddedResource,
                            Path = Path.Combine("content", "file.txt"),
                            PPOutputPath = "something"
                        }
                    }
                });

            var result = ExportSingle(description);
            result.EmbeddedResources.Should().HaveCount(1);

            var libraryAsset = result.EmbeddedResources.First();
            libraryAsset.Transform.Should().NotBeNull();
            libraryAsset.RelativePath.Should().Be(Path.Combine("content", "file.txt"));
            libraryAsset.ResolvedPath.Should().Be(Path.Combine(PackagePath, "content", "file.txt"));
        }

        [Fact]
        public void ExportsCompileContentFiles()
        {
            var description = CreateDescription(
                new LockFileTargetLibrary()
                {
                    ContentFiles = new List<LockFileContentFile>()
                    {
                        new LockFileContentFile()
                        {
                            BuildAction = BuildAction.Compile,
                            Path = Path.Combine("content", "file.cs"),
                            PPOutputPath = "something"
                        }
                    }
                });

            var result = ExportSingle(description);
            result.SourceReferences.Should().HaveCount(1);

            var libraryAsset = result.SourceReferences.First();
            libraryAsset.Transform.Should().NotBeNull();
            libraryAsset.RelativePath.Should().Be(Path.Combine("content", "file.cs"));
            libraryAsset.ResolvedPath.Should().Be(Path.Combine(PackagePath, "content", "file.cs"));
        }



        [Fact]
        public void SelectsContentFilesOfProjectCodeLanguage()
        {
            var description = CreateDescription(
                new LockFileTargetLibrary()
                {
                    ContentFiles = new List<LockFileContentFile>()
                    {
                            new LockFileContentFile()
                            {
                                BuildAction = BuildAction.Compile,
                                Path = Path.Combine("content", "file.cs"),
                                PPOutputPath = "something",
                                CodeLanguage = "cs"
                            },
                            new LockFileContentFile()
                            {
                                BuildAction = BuildAction.Compile,
                                Path = Path.Combine("content", "file.vb"),
                                PPOutputPath = "something",
                                CodeLanguage = "vb"
                            },
                            new LockFileContentFile()
                            {
                                BuildAction = BuildAction.Compile,
                                Path = Path.Combine("content", "file.any"),
                                PPOutputPath = "something",
                            }
                    }
                });

            var result = ExportSingle(description);
            result.SourceReferences.Should().HaveCount(1);

            var libraryAsset = result.SourceReferences.First();
            libraryAsset.Transform.Should().NotBeNull();
            libraryAsset.RelativePath.Should().Be(Path.Combine("content", "file.cs"));
            libraryAsset.ResolvedPath.Should().Be(Path.Combine(PackagePath, "content", "file.cs"));
        }

        [Fact]
        public void SelectsContentFilesWithNoLanguageIfProjectLanguageNotMathed()
        {
            var description = CreateDescription(
                new LockFileTargetLibrary()
                {
                    ContentFiles = new List<LockFileContentFile>()
                    {
                            new LockFileContentFile()
                            {
                                BuildAction = BuildAction.Compile,
                                Path = Path.Combine("content", "file.vb"),
                                PPOutputPath = "something",
                                CodeLanguage = "vb"
                            },
                            new LockFileContentFile()
                            {
                                BuildAction = BuildAction.Compile,
                                Path = Path.Combine("content", "file.any"),
                                PPOutputPath = "something",
                            }
                    }
                });

            var result = ExportSingle(description);
            result.SourceReferences.Should().HaveCount(1);

            var libraryAsset = result.SourceReferences.First();
            libraryAsset.Transform.Should().NotBeNull();
            libraryAsset.RelativePath.Should().Be(Path.Combine("content", "file.any"));
            libraryAsset.ResolvedPath.Should().Be(Path.Combine(PackagePath, "content", "file.any"));
        }

        [Fact]
        public void ExportsRuntimeTargets()
        {
            var win8Native = Path.Combine("native", "win8-x64", "Native.dll");
            var win8Runtime = Path.Combine("runtime", "win8-x64", "Runtime.dll");
            var linuxNative = Path.Combine("native", "linux", "Native.dll");

            var description = CreateDescription(
               new LockFileTargetLibrary()
               {
                   RuntimeTargets = new List<LockFileRuntimeTarget>()
                   {
                    new LockFileRuntimeTarget(
                        path: win8Native,
                        runtime: "win8-x64",
                        assetType: "native"
                    ),
                    new LockFileRuntimeTarget(
                        path: win8Runtime,
                        runtime: "win8-x64",
                        assetType: "runtime"
                    ),
                    new LockFileRuntimeTarget(
                        path: linuxNative,
                        runtime: "linux",
                        assetType: "native"
                    ),
                   }
               });

            var result = ExportSingle(description);
            result.RuntimeTargets.Should().HaveCount(2);

            var runtimeTarget = result.RuntimeTargets.Should().Contain(t => t.Runtime == "win8-x64").Subject;
            var runtime = runtimeTarget.RuntimeAssemblies.Single();
            runtime.RelativePath.Should().Be(win8Runtime);
            runtime.ResolvedPath.Should().Be(Path.Combine(PackagePath, win8Runtime));

            var native = runtimeTarget.NativeLibraries.Single();
            native.RelativePath.Should().Be(win8Native);
            native.ResolvedPath.Should().Be(Path.Combine(PackagePath, win8Native));

            runtimeTarget = result.RuntimeTargets.Should().Contain(t => t.Runtime == "linux").Subject;
            native = runtimeTarget.NativeLibraries.Single();
            native.RelativePath.Should().Be(linuxNative);
            native.ResolvedPath.Should().Be(Path.Combine(PackagePath, linuxNative));
        }

        private LibraryExport ExportSingle(LibraryDescription description = null)
        {
            var rootProject = new Project()
            {
                Name = "RootProject",
                CompilerName = "csc"
            };

            var rootProjectDescription = new ProjectDescription(
                new LibraryRange(),
                rootProject,
                new LibraryRange[] { },
                new TargetFrameworkInformation(),
                true);

            if (description == null)
            {
                description = rootProjectDescription;
            }
            else
            {
                description.Parents.Add(rootProjectDescription);
            }

            var libraryManager = new LibraryManager(new[] { description }, new DiagnosticMessage[] { }, "");
            var allExports = new LibraryExporter(rootProjectDescription, libraryManager, "config", "runtime", "basepath", "solutionroot").GetAllExports();
            var export = allExports.Single();
            return export;
        }

    }
}

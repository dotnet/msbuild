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

        private PackageDescription CreateDescription(LockFileTargetLibrary target = null, LockFilePackageLibrary package = null)
        {
            return new PackageDescription(PackagePath,
                package ?? new LockFilePackageLibrary(),
                target ?? new LockFileTargetLibrary(),
                new List<LibraryRange>(), true);
        }

        [Fact]
        private void ExportsPackageNativeLibraries()
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
        private void ExportsPackageCompilationAssebmlies()
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
        private void ExportsPackageRuntimeAssebmlies()
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
        private void ExportsSources()
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
        private void ExportsCopyToOutputContentFiles()
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
        private void ExportsResourceContentFiles()
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
        private void ExportsCompileContentFiles()
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
        private void SelectsContentFilesOfProjectCodeLanguage()
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
        private void SelectsContentFilesWithNoLanguageIfProjectLanguageNotMathed()
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
    }
}

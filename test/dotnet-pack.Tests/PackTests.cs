// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Pack.Tests
{
    public class PackTests : TestBase
    {
        [Fact(Skip="https://github.com/dotnet/cli/issues/4488")]
        public void OutputsPackagesToConfigurationSubdirWhenOutputParameterIsNotPassed()
        {
            var testInstance = TestAssets.Get("TestLibraryWithConfiguration")
                                         .CreateInstance()
                                         .WithSourceFiles()
                                         .WithRestoreFiles();

            var packCommand = new PackCommand(configuration: "Test")
                .WithWorkingDirectory(testInstance.Root);

            var result = packCommand.Execute();

            result.Should().Pass();

            var outputDir = testInstance.Root
                                        .GetDirectory("bin", "Test");

            outputDir.Should().Exist()
                          .And.HaveFiles(new [] 
                                            { 
                                                "TestLibraryWithConfiguration.1.0.0.nupkg", 
                                                "TestLibraryWithConfiguration.1.0.0.symbols.nupkg" 
                                            });
        }

        [Fact]
        public void OutputsPackagesFlatIntoOutputDirWhenOutputParameterIsPassed()
        {
            var testInstance = TestAssets.Get("TestLibraryWithConfiguration")
                .CreateInstance()
                .WithSourceFiles()
                .WithBuildFiles()
                .WithRestoreFiles();

            var outputDir = testInstance.Root
                                        .GetDirectory("bin2");

            var packCommand = new PackCommand(output: outputDir.FullName)
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            outputDir.Should().Exist()
                          .And.HaveFiles(new [] 
                                            { 
                                                "TestLibraryWithConfiguration.1.0.0.nupkg"
                                            });
        }

        [Fact]
        public void SettingVersionSuffixFlag_ShouldStampAssemblyInfoInOutputAssemblyAndPackage()
        {
            var testInstance = TestAssets.Get("TestLibraryWithConfiguration")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            new PackCommand(versionSuffix: "85")
                .WithWorkingDirectory(testInstance.Root.FullName)
                .WithConfiguration("Debug")
                .Execute()
                .Should().Pass();

            var output = testInstance.Root
                                     .GetDirectory("bin", "Debug", DefaultLibraryFramework)
                                     .GetFile("TestLibraryWithConfiguration.dll");
            
            var informationalVersion = PeReaderUtils.GetAssemblyAttributeValue(output.FullName, "AssemblyInformationalVersionAttribute");

            informationalVersion.Should().NotBeNull()
                                .And.BeEquivalentTo("1.0.0-85");

            var outputPackage = testInstance.Root
                                            .GetDirectory("bin", "Debug")
                                            .GetFile("TestLibraryWithConfiguration.1.0.0-85.nupkg");
            
            outputPackage.Should().Exist();
        }

        [Fact(Skip="https://github.com/dotnet/cli/issues/4486")]
        public void HasIncludedFiles()
        {
            var testInstance = TestAssets.Get("EndToEndTestApp")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .WithBuildFiles();

            new PackCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            var outputPackage = testInstance.Root
                                            .GetDirectory("bin", "Debug")
                                            .GetFile("EndToEndTestApp.1.0.0.nupkg");
            
            outputPackage.Should().Exist();

            ZipFile.Open(outputPackage.FullName, ZipArchiveMode.Read)
                .Entries
                .Should().Contain(e => e.FullName == "packfiles/pack1.txt")
                     .And.Contain(e => e.FullName == "newpath/pack2.txt")
                     .And.Contain(e => e.FullName == "anotherpath/pack2.txt");
        }

        [Fact(Skip="https://github.com/dotnet/cli/issues/4487")]
        public void PackAddsCorrectFilesForProjectsWithOutputNameSpecified()
        {
            var testInstance = TestAssets.Get("LibraryWithOutputAssemblyName")
                    .CreateInstance()
                    .WithSourceFiles()
                    .WithRestoreFiles();

            new PackCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            // netstandard1.5 is a workaround for https://github.com/dotnet/sdk/issues/318
            var outputPackage = testInstance.Root
                                            .GetDirectory("bin", "Debug", "netstandard1.5")
                                            .GetFile("LibraryWithOutputAssemblyName.1.0.0.nupkg");
            
            outputPackage.Should().Exist();

            ZipFile.Open(outputPackage.FullName, ZipArchiveMode.Read)
                .Entries
                .Should().Contain(e => e.FullName == "lib/netstandard1.5/MyLibrary.dll");

            var symbolsPackage = testInstance.Root
                                             .GetDirectory("bin", "Debug")
                                             .GetFile("LibraryWithOutputAssemblyName.1.0.0.symbols.nupkg");
            
            symbolsPackage.Should().Exist();

            ZipFile.Open(symbolsPackage.FullName, ZipArchiveMode.Read)
                .Entries
                .Should().Contain(e => e.FullName == "lib/netstandard1.5/MyLibrary.dll")
                     .And.Contain(e => e.FullName == "lib/netstandard1.5/MyLibrary.pdb");
        }

        [Theory]
        [InlineData("TestAppSimple")]
        [InlineData("FSharpTestAppSimple")]
        public void PackWorksWithLocalProject(string projectName)
        {
            var testInstance = TestAssets.Get(projectName)
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            new PackCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenPackaging()
        {
            var testInstance = TestAssets.Get("TestAppSimple")
                .CreateInstance()
                .WithSourceFiles();

            new PackCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ItDoesNotImplicitlyBuildAProjectWhenPackagingWithTheNoBuildOption()
        {
            var testInstance = TestAssets.Get("TestAppSimple")
                .CreateInstance()
                .WithSourceFiles();

            var result = new PackCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("--no-build");

            result.Should().Fail();
            if (!DotnetUnderTest.IsLocalized())
            {
                result.Should().NotHaveStdOutContaining("Restore")
                    .And.HaveStdOutContaining("project.assets.json");
            }
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenPackagingWithTheNoRestoreOption()
        {
            var testInstance = TestAssets.Get("TestAppSimple")
                .CreateInstance()
                .WithSourceFiles();

            new PackCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("--no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Fact]
        public void HasServiceableFlagWhenArgumentPassed()
        {
            var testInstance = TestAssets.Get("TestLibraryWithConfiguration")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles()
                .WithBuildFiles();

            var packCommand = new PackCommand(configuration: "Debug", serviceable: true)
                                    .WithWorkingDirectory(testInstance.Root);

            var result = packCommand.Execute();

            result.Should().Pass();

            var outputDir = testInstance.Root.GetDirectory("bin", "Debug");

            outputDir.Should().Exist()
                          .And.HaveFile("TestLibraryWithConfiguration.1.0.0.nupkg");

            var outputPackage = outputDir.GetFile("TestLibraryWithConfiguration.1.0.0.nupkg");

            var zip = ZipFile.Open(outputPackage.FullName, ZipArchiveMode.Read);

            zip.Entries.Should().Contain(e => e.FullName == "TestLibraryWithConfiguration.nuspec");

            var manifestReader = new StreamReader(zip.Entries.First(e => e.FullName == "TestLibraryWithConfiguration.nuspec").Open());

            var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());

            var node = nuspecXml.Descendants().Single(e => e.Name.LocalName == "serviceable");

            Assert.Equal("true", node.Value);
        }

        [Fact]
        public void ItPacksAppWhenRestoringToSpecificPackageDirectory()
        {
            var rootPath = TestAssets.CreateTestDirectory().FullName;
            var rootDir = new DirectoryInfo(rootPath);

            string dir = "pkgs";
            string args = $"--packages {dir}";

            string newArgs = $"console -o \"{rootPath}\" --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            new RestoreCommand()
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass();

            new PackCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput("--no-restore")
                .Should()
                .Pass();

            rootDir
                .GetDirectory("bin")
                .Should().HaveFilesMatching("*.nupkg", SearchOption.AllDirectories);
        }

        [Fact]
        public void ItDoesNotPrintCopyrightInfo()
        {
            var testInstance = TestAssets.Get("MSBuildTestApp")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            var result = new PackCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("--nologo");

            result.Should().Pass();

            if (!DotnetUnderTest.IsLocalized())
            {
                result.Should().NotHaveStdOutContaining("Copyright (C) Microsoft Corporation. All rights reserved.");
            }
        }

        private void CopyProjectToTempDir(string projectDir, TempDirectory tempDir)
        {
            // copy all the files to temp dir
            foreach (var file in Directory.EnumerateFiles(projectDir))
            {
                tempDir.CopyFile(file);
            }
        }

        private string GetProjectPath(TempDirectory projectDir)
        {
            return Path.Combine(projectDir.Path, "project.json");
        }
    }
}

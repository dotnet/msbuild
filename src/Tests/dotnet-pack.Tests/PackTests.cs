// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Compression;

namespace Microsoft.DotNet.Pack.Tests
{
    public class PackTests : SdkTest
    {
        public PackTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void OutputsPackagesToConfigurationSubdirWhenOutputParameterIsNotPassed()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestLibraryWithConfiguration")
                                         .WithSource();

            var packCommand = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path);

            var result = packCommand.Execute("-c", "Test");

            result.Should().Pass();

            var outputDir = new DirectoryInfo(Path.Combine(testInstance.Path, "bin", "Test"));

            outputDir.Should().Exist()
                          .And.HaveFiles(new[]
                                            {
                                                "TestLibraryWithConfiguration.1.0.0.nupkg"
                                            });
        }

        [Fact]
        public void OutputsPackagesFlatIntoOutputDirWhenOutputParameterIsPassed()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestLibraryWithConfiguration")
                .WithSource();

            var outputDir = new DirectoryInfo(Path.Combine(testInstance.Path, "bin2"));

            var packCommand = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("-o", outputDir.FullName)
                .Should().Pass();

            outputDir.Should().Exist()
                          .And.HaveFiles(new[]
                                            {
                                                "TestLibraryWithConfiguration.1.0.0.nupkg"
                                            });
        }

        [Fact]
        public void SettingVersionSuffixFlag_ShouldStampAssemblyInfoInOutputAssemblyAndPackage()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestLibraryWithConfiguration")
                .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--version-suffix", "85", "-c", "Debug")
                .Should().Pass();

            var output = new FileInfo(Path.Combine(testInstance.Path,
                                     "bin", "Debug", "netstandard1.5",
                                     "TestLibraryWithConfiguration.dll"));

            var informationalVersion = PeReaderUtils.GetAssemblyAttributeValue(output.FullName, "AssemblyInformationalVersionAttribute");

            informationalVersion.Should().NotBeNull()
                                .And.BeEquivalentTo("1.0.0-85");

            var outputPackage = new FileInfo(Path.Combine(testInstance.Path,
                                            "bin", "Debug",
                                            "TestLibraryWithConfiguration.1.0.0-85.nupkg"));

            outputPackage.Should().Exist();
        }

        [Fact(Skip = "Test project missing")]
        public void HasIncludedFiles()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("EndToEndTestApp")
                .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();

            var outputPackage = new FileInfo(Path.Combine(testInstance.Path,
                                            "bin", "Debug",
                                            "EndToEndTestApp.1.0.0.nupkg"));

            outputPackage.Should().Exist();

            ZipFile.Open(outputPackage.FullName, ZipArchiveMode.Read)
                .Entries
                .Should().Contain(e => e.FullName == "packfiles/pack1.txt")
                     .And.Contain(e => e.FullName == "newpath/pack2.txt")
                     .And.Contain(e => e.FullName == "anotherpath/pack2.txt");
        }

        [Fact(Skip = "Test project doesn't override assembly name")]
        public void PackAddsCorrectFilesForProjectsWithOutputNameSpecified()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("LibraryWithOutputAssemblyName")
                    .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();


            var outputPackage = new FileInfo(Path.Combine(testInstance.Path,
                                            "bin", "Debug",
                                            "LibraryWithOutputAssemblyName.1.0.0.nupkg"));

            outputPackage.Should().Exist();

            ZipFile.Open(outputPackage.FullName, ZipArchiveMode.Read)
                .Entries
                .Should().Contain(e => e.FullName == "lib/netstandard1.5/MyLibrary.dll");

            var symbolsPackage = new FileInfo(Path.Combine(testInstance.Path,
                                             "bin", "Debug",
                                             "LibraryWithOutputAssemblyName.1.0.0.symbols.nupkg"));

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
            var testInstance = _testAssetsManager.CopyTestAsset(projectName)
                .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenPackaging()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ItDoesNotImplicitlyBuildAProjectWhenPackagingWithTheNoBuildOption()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();

            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--no-build");

            result.Should().Fail();
            if (!TestContext.IsLocalized())
            {
                result.Should().NotHaveStdOutContaining("Restore")
                    .And.HaveStdOutContaining("project.assets.json");
            }
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenPackagingWithTheNoRestoreOption()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Fact]
        public void HasServiceableFlagWhenArgumentPassed()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestLibraryWithConfiguration")
                .WithSource();

            var packCommand = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path);

            var result = packCommand.Execute("-c", "Debug", "--serviceable");

            result.Should().Pass();

            var outputDir = new DirectoryInfo(Path.Combine(testInstance.Path, "bin", "Debug"));

            outputDir.Should().Exist()
                          .And.HaveFile("TestLibraryWithConfiguration.1.0.0.nupkg");

            var outputPackage = new FileInfo(Path.Combine(outputDir.FullName, "TestLibraryWithConfiguration.1.0.0.nupkg"));

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
            var rootPath = Path.Combine(_testAssetsManager.CreateTestDirectory().Path, "TestProject");
            Directory.CreateDirectory(rootPath);
            var rootDir = new DirectoryInfo(rootPath);

            string dir = "pkgs";

            new DotnetNewCommand(Log, "console", "-o", rootPath, "--no-restore")
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute()
                .Should()
                .Pass();

            new DotnetRestoreCommand(Log, rootPath)
                .Execute("--packages", dir)
                .Should()
                .Pass();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--no-restore")
                .Should()
                .Pass();

            new DirectoryInfo(Path.Combine(rootPath, "bin"))
                .Should().HaveFilesMatching("*.nupkg", SearchOption.AllDirectories);
        }

        [Fact]
        public void DotnetPackDoesNotPrintCopyrightInfo()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                .WithSource();

            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--nologo");

            result.Should().Pass();

            if (!TestContext.IsLocalized())
            {
                result.Should().NotHaveStdOutContaining("Copyright (C) Microsoft Corporation. All rights reserved.");
            }
        }

        [Fact]
        public void DotnetPackAcceptsRuntimeOption()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();

            var result = new DotnetPackCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--runtime", "unknown");

            result.Should().Fail()
                .And.HaveStdOutContaining("NETSDK1083");
        }
    }
}

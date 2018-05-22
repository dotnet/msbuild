// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.DotNet.MSBuildSdkResolver;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;
using System;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAnMSBuildSdkResolver : TestBase
    {
        private ITestOutputHelper _logger;

        public GivenAnMSBuildSdkResolver(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        [Fact]
        public void ItHasCorrectNameAndPriority()
        {
            var resolver = new DotNetMSBuildSdkResolver();

            Assert.Equal(5000, resolver.Priority);
            Assert.Equal("Microsoft.DotNet.MSBuildSdkResolver", resolver.Name);
        }

        [Fact]
        public void ItFindsTheVersionSpecifiedInGlobalJson()
        {
            var environment = new TestEnvironment();
            environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.97");
            var expected = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.98");
            environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.99");
            environment.CreateGlobalJson(environment.TestDirectory, "99.99.98");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, null),
                new MockContext { ProjectFilePath = environment.TestDirectory.FullName },
                new MockFactory());

            result.Success.Should().BeTrue();
            result.Path.Should().Be(expected.FullName);
            result.Version.Should().Be("99.99.98");
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ItReturnsNullIfTheVersionFoundDoesNotSatisfyTheMinVersion()
        {
            var environment = new TestEnvironment();
            environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.99");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "999.99.99"),
                new MockContext { ProjectFilePath = environment.TestDirectory.FullName },
                new MockFactory());

            result.Success.Should().BeFalse();
            result.Path.Should().BeNull();
            result.Version.Should().BeNull();
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().Contain("Version 99.99.99 of the .NET Core SDK is smaller than the minimum version 999.99.99"
                + " requested. Check that a recent enough .NET Core SDK is installed, increase the minimum version"
                + " specified in the project, or increase the version specified in global.json.");
        }

        [Fact]
        public void ItReturnsNullWhenTheSDKRequiresAHigherVersionOfMSBuildThanTheOneAvailable()
        {
            var environment = new TestEnvironment();
            var expected =
                environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.99", new Version(2, 0));
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "99.99.99"),
                new MockContext
                {
                    MSBuildVersion = new Version(1, 0),
                    ProjectFilePath = environment.TestDirectory.FullName
                },
                new MockFactory());

            result.Success.Should().BeFalse();
            result.Path.Should().BeNull();
            result.Version.Should().BeNull();
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().Contain("Version 99.99.99 of the .NET Core SDK requires at least version 2.0 of MSBuild."
                + " The current available version of MSBuild is 1.0. Change the .NET Core SDK specified in global.json to an older"
                + " version that requires the MSBuild version currently available.");
        }

        [Fact]
        public void ItReturnsNullWhenTheDefaultVSRequiredSDKVersionIsHigherThanTheSDKVersionAvailable()
        {
            var environment = new TestEnvironment();
            var expected =
                environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "1.0.1");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "1.0.0"),
                new MockContext { ProjectFilePath = environment.TestDirectory.FullName },
                new MockFactory());

            result.Success.Should().BeFalse();
            result.Path.Should().BeNull();
            result.Version.Should().BeNull();
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().Contain($"Version 1.0.1 of the .NET Core SDK is smaller than the minimum version"
                            + " 1.0.4 required by Visual Studio. Check that a recent enough"
                            + " .NET Core SDK is installed or increase the version specified in global.json.");
        }

        [Fact]
        public void ItReturnsNullWhenTheTheVSRequiredSDKVersionIsHigherThanTheSDKVersionAvailable()
        {
            var environment = new TestEnvironment();
            var expected =
                environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "1.0.1");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.CreateMinimumVSDefinedSDKVersionFile("2.0.0");

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "1.0.0"),
                new MockContext { ProjectFilePath = environment.TestDirectory.FullName },
                new MockFactory());

            result.Success.Should().BeFalse();
            result.Path.Should().BeNull();
            result.Version.Should().BeNull();
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().Contain($"Version 1.0.1 of the .NET Core SDK is smaller than the minimum version"
                            + " 2.0.0 required by Visual Studio. Check that a recent enough"
                            + " .NET Core SDK is installed or increase the version specified in global.json.");
        }

        [Fact]
        public void ItReturnsTheVersionIfItIsEqualToTheMinVersionAndTheVSDefinedMinVersion()
        {
            var environment = new TestEnvironment();
            var expected = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "99.99.99");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.CreateMinimumVSDefinedSDKVersionFile("99.99.99");

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "99.99.99"),
                new MockContext { ProjectFilePath = environment.TestDirectory.FullName },
                new MockFactory());

            result.Success.Should().BeTrue();
            result.Path.Should().Be(expected.FullName);
            result.Version.Should().Be("99.99.99");
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        [Fact]
        public void ItReturnsTheVersionIfItIsHigherThanTheMinVersionAndTheVSDefinedMinVersion()
        {
            var environment = new TestEnvironment();
            var expected = environment.CreateSdkDirectory(ProgramFiles.X64, "Some.Test.Sdk", "999.99.99");
            environment.CreateMuxerAndAddToPath(ProgramFiles.X64);
            environment.CreateMinimumVSDefinedSDKVersionFile("999.99.98");

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, "99.99.99"),
                new MockContext { ProjectFilePath = environment.TestDirectory.FullName },
                new MockFactory());

            result.Success.Should().BeTrue();
            result.Path.Should().Be(expected.FullName);
            result.Version.Should().Be("999.99.99");
            result.Warnings.Should().BeNullOrEmpty();
            result.Errors.Should().BeNullOrEmpty();
        }

        private enum ProgramFiles
        {
            X64,
            X86,
            Default,
        }

        private sealed class TestEnvironment : SdkResolverContext
        {
            public string Muxer => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";

            public string PathEnvironmentVariable { get; set; }

            public DirectoryInfo TestDirectory { get; }

            public TestEnvironment(string identifier = "", [CallerMemberName] string callingMethod = "")
            {
                TestDirectory = TestAssets.CreateTestDirectory(
                    "temp",
                    identifier: identifier,
                    callingMethod: callingMethod);

                DeleteMinimumVSDefinedSDKVersionFile();

                PathEnvironmentVariable = string.Empty;
            }

            public SdkResolver CreateResolver()
                => new DotNetMSBuildSdkResolver(GetEnvironmentVariable);

            public DirectoryInfo GetSdkDirectory(ProgramFiles programFiles, string sdkName, string sdkVersion)
                => TestDirectory.GetDirectory(
                    GetProgramFilesDirectory(programFiles).FullName,
                    "dotnet",
                    "sdk",
                    sdkVersion,
                    "Sdks",
                    sdkName,
                    "Sdk");

            public DirectoryInfo GetProgramFilesDirectory(ProgramFiles programFiles)
                => TestDirectory.GetDirectory($"ProgramFiles{programFiles}");
            
            public DirectoryInfo CreateSdkDirectory(
                ProgramFiles programFiles,
                string sdkName,
                string sdkVersion,
                Version minimumMSBuildVersion = null)
            {
                var dir = GetSdkDirectory(programFiles, sdkName, sdkVersion);
                dir.Create();

                if (minimumMSBuildVersion != null)
                {
                    CreateMSBuildRequiredVersionFile(programFiles, sdkVersion, minimumMSBuildVersion);
                }

                return dir;
            }

            public void CreateMuxerAndAddToPath(ProgramFiles programFiles)
            {
                var muxerDirectory =
                    TestDirectory.GetDirectory(GetProgramFilesDirectory(programFiles).FullName, "dotnet");

                new FileInfo(Path.Combine(muxerDirectory.FullName, Muxer)).Create();

                PathEnvironmentVariable = $"{muxerDirectory}{Path.PathSeparator}{PathEnvironmentVariable}";
            }

            private void CreateMSBuildRequiredVersionFile(
                ProgramFiles programFiles,
                string sdkVersion,
                Version minimumMSBuildVersion)
            {
                if (minimumMSBuildVersion == null)
                {
                    minimumMSBuildVersion = new Version(1, 0);
                }

                var cliDirectory = TestDirectory.GetDirectory(
                    GetProgramFilesDirectory(programFiles).FullName,
                    "dotnet",
                    "sdk",
                    sdkVersion);

                File.WriteAllText(
                    Path.Combine(cliDirectory.FullName, "minimumMSBuildVersion"),
                    minimumMSBuildVersion.ToString());
            }

            public void CreateGlobalJson(DirectoryInfo directory, string version)
                => File.WriteAllText(directory.GetFile("global.json").FullName, 
                    $@"{{ ""sdk"": {{ ""version"":  ""{version}"" }} }}");

            public string GetEnvironmentVariable(string variable)
            {
                switch (variable)
                {
                    case "PATH":
                        return PathEnvironmentVariable;
                    default:
                        return null;
                }
            }

            public void CreateMinimumVSDefinedSDKVersionFile(string version)
            {
                File.WriteAllText(GetMinimumVSDefinedSDKVersionFilePath(), version);
            }

            private void DeleteMinimumVSDefinedSDKVersionFile()
            {                
                File.Delete(GetMinimumVSDefinedSDKVersionFilePath());
            }

            private string GetMinimumVSDefinedSDKVersionFilePath()
            {
                string baseDirectory = AppContext.BaseDirectory;
                return Path.Combine(baseDirectory, "minimumVSDefinedSDKVersion");
            }
        }

        private sealed class MockContext : SdkResolverContext
        {
            public new string ProjectFilePath { get => base.ProjectFilePath; set => base.ProjectFilePath = value; }
            public new string SolutionFilePath { get => base.SolutionFilePath; set => base.SolutionFilePath = value; }
            public new Version MSBuildVersion { get => base.MSBuildVersion; set => base.MSBuildVersion = value; }

            public MockContext()
            {
                MSBuildVersion = new Version(15, 3, 0);
            }
        }

        private sealed class MockFactory : SdkResultFactory
        {
            public override SdkResult IndicateFailure(IEnumerable<string> errors, IEnumerable<string> warnings = null)
                => new MockResult(success: false, path: null, version: null, warnings: warnings, errors: errors);

            public override SdkResult IndicateSuccess(string path, string version, IEnumerable<string> warnings = null)
                => new MockResult(success: true, path: path, version: version, warnings: warnings);
        }

        private sealed class MockResult : SdkResult
        {
            public MockResult(bool success, string path, string version, IEnumerable<string> warnings = null,
                IEnumerable<string> errors = null)
            {
                Success = success;
                Path = path;
                Version = version;
                Warnings = warnings;
                Errors = errors;
            }

            public new bool Success
            {
                get => base.Success;
                private set => base.Success = value;
            }

            public override string Version { get; protected set; }
            public override string Path { get; protected set; }
            public IEnumerable<string> Errors { get; }
            public IEnumerable<string> Warnings { get; }
        }
    }
}

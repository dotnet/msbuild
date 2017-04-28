// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.DotNet.MSBuildSdkResolver;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

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

            var resolver = environment.CreateResolver();
            var result = (MockResult)resolver.Resolve(
                new SdkReference("Some.Test.Sdk", null, null),
                new MockContext { ProjectFilePath = environment.TestDirectory.FullName },
                new MockFactory());

            result.Success.Should().Be(true);
            result.Path.Should().Be(expected.FullName);
            result.Version.Should().Be("99.99.98");
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
            public DirectoryInfo TestDirectory { get; }

            public TestEnvironment(string identifier = "", [CallerMemberName] string callingMethod = "")
            {
                TestDirectory = TestAssets.CreateTestDirectory(
                    "temp",
                    identifier: identifier,
                    callingMethod: callingMethod);
            }

            public SdkResolver CreateResolver()
                => new DotNetMSBuildSdkResolver(
                new Dictionary<string, string>
                {
                    ["ProgramW6432"] = GetProgramFilesDirectory(ProgramFiles.X64).FullName,
                    ["ProgramFiles(x86)"] = GetProgramFilesDirectory(ProgramFiles.X86).FullName,
                    ["ProgramFiles"] = GetProgramFilesDirectory(ProgramFiles.Default).FullName,
                });

            public DirectoryInfo GetSdkDirectory(ProgramFiles programFiles, string sdkName, string sdkVersion)
                => TestDirectory.GetDirectory(GetProgramFilesDirectory(programFiles).FullName, "dotnet", "sdk", sdkVersion, "Sdks", sdkName, "Sdk");

            public DirectoryInfo GetProgramFilesDirectory(ProgramFiles programFiles)
                => TestDirectory.GetDirectory($"ProgramFiles{programFiles}");
            
            public DirectoryInfo CreateSdkDirectory(ProgramFiles programFiles, string sdkVersion, string sdkName)
            {
                var dir = GetSdkDirectory(programFiles, sdkVersion, sdkName);
                dir.Create();
                return dir;
            }

            public void CreateGlobalJson(DirectoryInfo directory, string version)
                => File.WriteAllText(directory.GetFile("global.json").FullName, 
                    $@"{{ ""sdk"": {{ ""version"":  ""{version}"" }} }}");
        }

        private sealed class MockContext : SdkResolverContext
        {
            public new string ProjectFilePath { get => base.ProjectFilePath; set => base.ProjectFilePath = value; }
            public new string SolutionFilePath { get => base.SolutionFilePath; set => base.SolutionFilePath = value; }
        }

        private sealed class MockFactory : SdkResultFactory
        {
            public override SdkResult IndicateFailure(IEnumerable<string> errors, IEnumerable<string> warnings = null)
                => new MockResult { Success = false, Errors = errors, Warnings = warnings  };

            public override SdkResult IndicateSuccess(string path, string version, IEnumerable<string> warnings = null)
                => new MockResult { Success = true, Path = path, Version = version, Warnings = warnings };
        }

        private sealed class MockResult : SdkResult
        {
            public new bool Success { get => base.Success; set => base.Success = value; }
            public string Version { get; set; }
            public string Path { get; set; }
            public IEnumerable<string> Errors { get; set; }
            public IEnumerable<string> Warnings { get; set; }
        }
    }
}

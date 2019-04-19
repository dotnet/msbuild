// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;
using System;
using System.IO;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishASingleFileApp : SdkTest
    {
        private const string TestProjectName = "HelloWorldWithSubDirs";

        private const string PublishSingleFile = "/p:PublishSingleFile=true";
        private const string FrameworkDependent = "/p:SelfContained=false";
        private const string IncludePdb = "/p:IncludeSymbolsInSingleFile=true";
        private const string ExcludeContent = "/p:ExcludeContent=true";
        private const string DontUseAppHost = "/p:UseAppHost=false";
        private const string ReadyToRun = "/p:PublishReadyToRun=true";

        private readonly string RuntimeIdentifier = $"/p:RuntimeIdentifier={RuntimeEnvironment.GetRuntimeIdentifier()}";
        private readonly string SingleFile = $"{TestProjectName}{Constants.ExeSuffix}";
        private readonly string PdbFile = $"{TestProjectName}.pdb";
        private const string ContentFile = "Signature.stamp";

        public GivenThatWeWantToPublishASingleFileApp(ITestOutputHelper log) : base(log)
        {
        }

        private PublishCommand GetPublishCommand()
        {
            var testAsset = _testAssetsManager
               .CopyTestAsset(TestProjectName)
               .WithSource();
            testAsset.Restore(Log, testAsset.TestRoot, RuntimeIdentifier);

            return new PublishCommand(Log, testAsset.TestRoot);
        }

        private DirectoryInfo GetPublishDirectory(PublishCommand publishCommand)
        {
            return publishCommand.GetOutputDirectory(targetFramework: "netcoreapp3.0",
                                                     runtimeIdentifier: RuntimeEnvironment.GetRuntimeIdentifier());
        }

        [Fact]
        public void It_errors_when_publishing_single_file_app_without_rid()
        {
            GetPublishCommand()
                .Execute(PublishSingleFile)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutRuntimeIdentifier);
        }

        [Fact]
        public void It_errors_when_publishing_single_file_without_apphost()
        {
            GetPublishCommand()
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent, DontUseAppHost)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutAppHost);
        }

        [Fact]
        public void It_errors_when_publishing_single_file_lib()
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileClassLib",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true,
                IsExe = false,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name, RuntimeIdentifier);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutExecutable);
        }

        [Fact]
        public void It_generates_a_single_file_for_framework_dependent_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_generates_a_single_file_for_self_contained_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_generates_a_single_file_including_pdbs()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludePdb)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_generates_a_single_file_excluding_content()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, ExcludeContent)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, ContentFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_generates_a_single_file_for_R2R_compiled_Apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, ReadyToRun)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_does_not_rewrite_the_single_file_unnecessarily()
        {
            var publishCommand = GetPublishCommand();
            var singleFilePath = Path.Combine(GetPublishDirectory(publishCommand).FullName, SingleFile);

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            DateTime fileWriteTimeAfterFirstRun = File.GetLastWriteTimeUtc(singleFilePath);

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            DateTime fileWriteTimeAfterSecondRun = File.GetLastWriteTimeUtc(singleFilePath);

            fileWriteTimeAfterSecondRun.Should().Be(fileWriteTimeAfterFirstRun);
        }

    }
}
